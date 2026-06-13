using System.Net.Http;
using Blurt.Core;

namespace Blurt.App;

/// <summary>
/// Hosts the application as a tray-only process: no main window, just a
/// <see cref="NotifyIcon"/> with a context menu. This is the lifecycle anchor
/// the rest of Blurt (hotkeys, overlay, settings) plugs into. It owns the
/// keyboard hook and surfaces a visible signal on each trigger down/up.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;

    // Status feedback (issue 06): the click-through overlay pill and the colour-
    // coded tray icons, both driven from Core's pure OverlayState/TrayState/
    // TrayPalette so listening/transcribing show in sync on overlay and tray.
    // Overlay and sound are not readonly: the settings window (issue 14) updates
    // them live on save (the anchor by swapping in a new controller).
    private OverlayController _overlay;
    private readonly TrayIcons _trayIcons = new();
    private bool _soundEnabled;

    // The single fail-soft surface (issue 13): every notice — outcome balloons,
    // provisioning failures, the no-mic warning, flex-slot hints — goes through
    // here instead of calling ShowBalloonTip directly, so they share one
    // non-blocking channel. Tray-only today; the overlay channel (issue 06)
    // plugs into TrayNotifier without touching these call sites.
    private readonly INotifier _notifier;

    // Not readonly: a hotkey remap (issue 14) disposes this hook and installs a
    // fresh one built from the new bindings, so remapped triggers take effect live.
    private KeyboardHook _keyboardHook;

    // Single live settings window (issue 14): reopening brings the existing one to
    // the front instead of stacking duplicates. Null when closed.
    private SettingsWindow? _settingsWindow;

    private readonly TextInjector _textInjector;
    private readonly AudioRecorder _recorder = new();

    // Fix mode (issue 09): settings supply the refiner's base URL/model/key, and
    // a single long-lived HttpClient is reused across utterances (creating one
    // per request would leak sockets). The refiner itself is built per Fix so a
    // settings change takes effect without an app restart.
    private readonly SettingsStore _settings = new(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        new DpapiSecretProtector());
    private readonly HttpClient _httpClient = new();

    // Recent-dictations history (issue 26): the last few final texts, newest
    // first, RAM-only — recovers a paste that landed in the void. Surfaced as a
    // tray submenu whose entries copy back to the clipboard.
    private readonly RecentDictations _recentDictations = new();

    // Flex-slot state (issue 07). The classifier turns the key's hold duration
    // into tap-vs-hold; the cycle rotates Pur → Bullets → Custom on each tap.
    // Both are pure Core logic; this glue only measures time and dispatches.
    private readonly TapHoldClassifier _tapHoldClassifier = new();
    private readonly FlexSlotCycle _flexSlotCycle = new();

    // When the Flex-slot key went down, so its release can be classified as a
    // tap (cycle the mode) or a hold (dictate). Null between presses.
    private long? _flexSlotDownTicks;

    // The "also translate to English" modifier for the in-flight dictation (issue 39):
    // captured from the trigger's Down event (Shift held with the chord) and consumed
    // when that trigger's Up completes the dictation. Per-dictation, never persisted.
    private bool _alsoTranslate;

    // Created lazily on the first dictation so the model download (first run
    // only) never happens before the user actually asks for a transcription.
    // AsyncLazy forgets failed attempts: a failed download (e.g. blocked
    // network) is retried on the next dictation instead of poisoning every
    // attempt until app restart.
    private readonly AsyncLazy<LocalWhisper> _transcriber;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        var recentMenu = new ToolStripMenuItem("Recent dictations");
        menu.Items.Add(recentMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("How to use Blurt…", image: null, (_, _) => RunTutorial());
        menu.Items.Add("Settings…", image: null, (_, _) => OpenSettings());
        menu.Items.Add("Exit", image: null, (_, _) => ExitApp());

        // Rebuild the history submenu each time the menu opens, so it always
        // shows the current ring-buffer contents (issue 26).
        menu.Opening += (_, _) => PopulateRecentDictations(recentMenu);

        // First-run onboarding (issue 15): if no setup has been completed yet (fresh
        // install → Default config with OnboardingCompleted=false), walk the guided
        // wizard before the tray goes quiet. The flag it persists is the single
        // source of truth, so this never runs again once finished. WPF shown modally
        // on this WinForms STA thread (no second System.Windows.Application), exactly
        // like the overlay/settings window. After it returns we reload so the saved
        // config (e.g. a key, OnboardingCompleted=true) feeds the rest of start-up.
        var config = _settings.Load();
        if (Onboarding.IsNeeded(config))
        {
            RunOnboarding();
            config = _settings.Load();

            // First run teaches as well as configures (issue 40): right after the
            // setup wizard, play the animated tutorial once so a newcomer learns
            // push-to-talk, the triggers and the Flex modes. Replayable later from
            // the tray ("How to use Blurt…"); a returning user never sees it.
            RunTutorial();
        }

        // Read the overlay anchor and sound toggle at start-up. Both are now applied
        // live by the settings window (issue 14), but still loaded here as the
        // starting state.
        _soundEnabled = config.SoundEnabled;
        _overlay = new OverlayController(config.OverlayAnchor);

        _trayIcon = new NotifyIcon
        {
            // Colour-coded status icon (issue 06): start idle (neutral grey dot).
            Icon = _trayIcons.For(TrayState.Idle),
            Text = AppInfo.Name,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifier = new TrayNotifier(_trayIcon);

        // 300 ms gives the focused app time to consume the Ctrl+V before the
        // user's original clipboard is put back (pasting is asynchronous).
        _textInjector = new TextInjector(
            new WinFormsClipboard(),
            new SendInputPasteKeystroke(),
            postPasteDelay: () => Task.Delay(TimeSpan.FromMilliseconds(300)));

        _transcriber = new AsyncLazy<LocalWhisper>(ProvisionTranscriberAsync);

        // Build the hook from the persisted hotkey bindings (issue 14): the resolver
        // gets the configured VK→trigger map, falling back to defaults for any
        // missing/garbage entry, so a remapped chord works from launch.
        _keyboardHook = InstallHook(config);

        // Dev affordance for visual verification (issue 19): `Blurt.exe --settings`
        // or `--onboarding` opens that surface straight away, so UI checks and
        // screenshots don't have to click through the tray menu first.
        var launchArgs = Environment.GetCommandLineArgs();
        if (launchArgs.Contains("--settings"))
            OpenSettings();
        else if (launchArgs.Contains("--onboarding"))
            RunOnboarding();
        else if (launchArgs.Contains("--tutorial"))
            RunTutorial();
        else if (launchArgs.Contains("--overlay"))
            _overlay.Show(OverlayState.Listening);
        else if (launchArgs.Contains("--traymenu"))
        {
            // Seed the history with sample entries and pop the menu, so the
            // recent-dictations submenu (issue 26) can be checked without
            // dictating three times first.
            _recentDictations.Add("Erstes Diktat — ein kurzer Satz.");
            _recentDictations.Add("- erstens\n- zweitens\n- drittens");
            _recentDictations.Add("Dies ist ein sehr langes drittes Diktat, das deutlich über die achtundvierzig Zeichen der Vorschau hinausgeht.");
            menu.Show(new System.Drawing.Point(300, 300));
        }
    }

    // Create, wire, and install a keyboard hook whose resolver uses the hotkey
    // bindings from the given config. Used at start-up and again whenever the
    // settings window remaps the hotkeys (the old hook is disposed first).
    private KeyboardHook InstallHook(BlurtConfig config)
    {
        var resolver = new TriggerResolver(HotkeyBindings.ResolveVkMap(config));
        var hook = new KeyboardHook(resolver);
        hook.TriggerObserved += OnTriggerObserved;
        hook.Install();
        return hook;
    }

    // Pure dispatcher: each trigger owns its own handler so their record/
    // transcribe lifecycles stay independent — English = Pur push-to-talk
    // (issue 05), Fix = refined push-to-talk (issue 09), Flex-slot =
    // tap-to-cycle / hold-to-dictate with the current mode (issue 07).
    private void OnTriggerObserved(TriggerEvent trigger)
    {
        // Fail-soft (design §10): this runs inside the low-level keyboard-hook callback,
        // where an escaping exception kills the whole process (it bypasses WinForms'
        // ThreadException). A dictation hiccup must never do that — catch it, tell the
        // user, drop any half-recorded take, and reset so the next press starts clean.
        try
        {
            DispatchTrigger(trigger);
        }
        catch (Exception ex)
        {
            _notifier.Notify($"Dictation error — skipped this one. ({ex.Message})", NoticeLevel.Error);
            try { _recorder.Discard(); } catch { /* best-effort cleanup */ }
            _flexSlotDownTicks = null;
            ReturnToIdle();
        }
    }

    private void DispatchTrigger(TriggerEvent trigger)
    {
        // Capture the also-translate modifier at press time (issue 39): the Shift state
        // is read when the chord goes down, and the handlers consume it on key-up when
        // they process the take. Per-dictation, so it's never persisted.
        if (trigger.Edge == KeyEdge.Down)
        {
            _alsoTranslate = trigger.AlsoTranslate;
        }

        if (trigger.Kind == TriggerKind.English)
        {
            OnEnglishTrigger(trigger.Edge);
            return;
        }

        if (trigger.Kind == TriggerKind.Fix)
        {
            OnFixTrigger(trigger.Edge);
            return;
        }

        if (trigger.Kind == TriggerKind.FlexSlot)
        {
            OnFlexSlotTrigger(trigger.Edge);
            return;
        }
    }

    // Fail-soft microphone start (design §10, mode 1): opening the capture device
    // throws (NAudio) when there is no microphone or permission is denied. Catch it,
    // surface a notice through the notifier, and return false so the down-handler
    // simply doesn't enter recording — the app keeps running instead of crashing.
    //
    // Device selection (issue 16): read the configured InputDeviceMode + saved name
    // and hand them to the recorder, which resolves them (Core's InputDeviceResolver)
    // against the devices NAudio currently enumerates. FollowDefault re-opens the
    // Windows default each press, so a newly-plugged Bluetooth headset just works.
    // If a saved Specific device is gone, the resolver falls back to the default and
    // flags it — surface that as a one-line warning while still recording.
    private bool TryStartRecording()
    {
        var config = _settings.Load();
        try
        {
            var resolution = _recorder.Start(config.InputDeviceMode, config.InputDeviceName);
            if (resolution.FellBack)
            {
                _notifier.Notify(
                    $"Microphone \"{config.InputDeviceName}\" not found — using the default device.",
                    NoticeLevel.Warning);
            }
            return true;
        }
        catch (Exception ex)
        {
            _notifier.Notify($"No microphone available: {ex.Message}", NoticeLevel.Error);
            return false;
        }
    }

    // English push-to-talk (issue 10): hold = record, release = transcribe the
    // (German) speech locally then refine it through the OpenAI-compatible
    // endpoint with the translation prompt, injecting fluent English at the
    // cursor. Shares RefineAndInjectAsync with Fix — only the prompt differs —
    // and falls soft back to the raw transcript when the endpoint is unreachable.
    private void OnEnglishTrigger(KeyEdge edge)
    {
        if (edge == KeyEdge.Down)
        {
            if (!TryStartRecording())
            {
                return;   // no mic — notice already shown, app keeps running
            }

            _trayIcon.Text = $"{AppInfo.Name} - recording (english)";
            EnterRecording();   // "listening" pill + red tray + optional start beep
            return;
        }

        _trayIcon.Text = AppInfo.Name;
        if (!_recorder.IsRecording)
        {
            ReturnToIdle();   // key-up without a matching down — make sure we rest
            return;
        }

        var audio = _recorder.Stop();
        PlaySound(start: false);   // optional stop beep at release
        // Read the editable English prompt fresh per dictation (issue 35) so a
        // Settings edit takes effect without a restart; blank falls back to default.
        var englishPrompt = ModePrompts.For(RefinedMode.English, _settings.Load());
        // Issue 39: the also-translate modifier composes onto English too (a redundant
        // but harmless English-on-English layer); English always has a prompt, so the
        // composed result is never null.
        var prompt = TranslationModifier.Compose(englishPrompt, _alsoTranslate)!;
        var label = _alsoTranslate ? StatusLabel.AlsoEnglish(StatusLabel.Translating) : StatusLabel.Translating;
        _ = RefineAndInjectAsync(audio, prompt, label);   // fire-and-forget; outcome surfaces as a balloon only when notable
    }

    // Flex-slot push-to-talk (issue 07 + 11). Down starts recording and stamps the
    // press time; up measures the held duration and lets TapHoldClassifier
    // decide: a tap discards the take and cycles the mode (tray shows the new
    // one), a hold transcribes with whichever mode is selected — Pur verbatim,
    // Bullets/Custom through the refiner (FlexSlotPrompts picks the prompt).
    private void OnFlexSlotTrigger(KeyEdge edge)
    {
        if (edge == KeyEdge.Down)
        {
            if (!TryStartRecording())
            {
                // No mic — notice already shown. Leave _flexSlotDownTicks null so
                // the matching key-up is treated as a no-op press, not a tap/hold.
                return;
            }

            _flexSlotDownTicks = Environment.TickCount64;
            _trayIcon.Text = $"{AppInfo.Name} - {_flexSlotCycle.Current} (recording)";
            EnterRecording();   // "listening" pill + red tray + optional start beep
            return;
        }

        _trayIcon.Text = AppInfo.Name;

        if (_flexSlotDownTicks is not { } downTicks || !_recorder.IsRecording)
        {
            // Key-up without a matching down (e.g. held across app start). Drop
            // any partial take and reset so the next press starts cleanly.
            _flexSlotDownTicks = null;
            _recorder.Discard();   // non-blocking; no-op when not recording (issue 21)
            ReturnToIdle();   // make sure the pill/tray don't get stuck recording
            return;
        }

        var held = TimeSpan.FromMilliseconds(Environment.TickCount64 - downTicks);
        _flexSlotDownTicks = null;

        // A tap or a hold both end the recording, so play the optional stop beep
        // once here before either branch acts.
        PlaySound(start: false);

        if (_tapHoldClassifier.Classify(held) == TapOrHold.Tap)
        {
            // Tap: the recording was never meant as speech — throw it away and
            // advance the mode. Discard (not Stop) so the UI thread never waits on
            // the device draining a take we're binning — the cycle must feel
            // instant (issue 21).
            _recorder.Discard();
            var mode = _flexSlotCycle.Cycle();
            SetTrayState(TrayState.Idle);   // no transcription; tray rests
            _trayIcon.Text = $"{AppInfo.Name} - {mode}";

            // Feedback goes through the overlay pill, NOT a tray balloon: Windows
            // throttles successive balloons, so a quick second tap showed no change
            // and the cycle felt stuck until the old balloon timed out. The overlay
            // updates instantly and per-mode distinctly (issue: flex feedback).
            _overlay.FlashMode(mode);
            return;
        }

        // Hold: dictate with the current mode. FlexSlotPrompts resolves the system
        // prompt for the mode: Pur and an unset Custom yield null ("no refiner"),
        // so those go through verbatim DictateAsync (zero network); Bullets and a
        // configured Custom carry a prompt and run RefineAndInjectAsync.
        var audio = _recorder.Stop();
        var currentMode = _flexSlotCycle.Current;
        // Issue 39: layer an English translation on top when the modifier was held.
        // Compose leaves the verbatim path null (Pur / empty Custom stay zero-network),
        // so the Shift modifier can never turn Pur into a network call.
        var prompt = TranslationModifier.Compose(
            FlexSlotPrompts.For(currentMode, _settings.Load()), _alsoTranslate);

        if (prompt is null)
        {
            // No refiner for this mode. Custom with an empty prompt still inserts
            // the raw transcript (fail-soft) but first nudges the user to set one.
            if (currentMode == FlexSlotMode.Custom)
            {
                _notifier.Notify(
                    "No custom prompt set — inserting raw dictation.", NoticeLevel.Info);
            }

            _ = DictateAsync(audio);   // verbatim, no refinement (same as English/Pur)
        }
        else
        {
            // Bullets, Email and Custom all refine, but the status pill names which.
            var refiningLabel = currentMode switch
            {
                FlexSlotMode.Bullets => StatusLabel.Bulleting,
                FlexSlotMode.Email => StatusLabel.Emailing,
                _ => StatusLabel.Refining,
            };
            // Issue 39: when the modifier is layered on, the pill shows it (e.g.
            // "bulleting → english") so the extra English step is visible.
            if (_alsoTranslate)
                refiningLabel = StatusLabel.AlsoEnglish(refiningLabel);
            _ = RefineAndInjectAsync(audio, prompt, refiningLabel);   // Bullets / Email / configured Custom (+ optional English layer)
        }
    }

    private async Task DictateAsync(Stream audio)
    {
        // This method starts on the UI thread (the hook delivers events there),
        // so every await resumes on it — balloon calls below are safe.
        // Verbatim/Pur is always local (zeroNetwork below), so the status says so.
        EnterProcessing(StatusLabel.Transcribing(local: true));
        try
        {
            // Provisioning (first-run download) can fail on a blocked network;
            // keep that a soft notice rather than letting it reach the pipeline.
            // zeroNetwork: this is the verbatim (Pur / raw-fallback) path — its
            // offline promise means the resolver keeps it on local whisper.cpp
            // even when Online transcription is configured (issue 12).
            var transcriber = await ResolveTranscriberAsync(zeroNetwork: true);

            // Pur mode: no refinement delegate, so verbatim Whisper output is
            // injected at the cursor.
            var pipeline = new DictationPipeline(
                transcriber,
                _textInjector,
                onResult: _recentDictations.Add);   // issue 26: recoverable history

            var outcome = await pipeline.RunAsync(audio);

            // Single fail-soft path: DictationNotices decides what (if anything)
            // to say for this outcome — Injected is silent, every other case maps
            // to a notice surfaced through the one notifier.
            Notify(outcome);
        }
        catch (Exception ex)
        {
            // Provisioning failure (e.g. blocked model download) — fail-soft, and
            // point at the manual install for the selected model (issue 22).
            _notifier.Notify(ProvisioningFailureNotice(ex.Message), NoticeLevel.Error);
        }
        finally
        {
            ReturnToIdle();   // hide the pill + tray back to idle once text is in (or failed)
            await audio.DisposeAsync();
        }
    }

    // Fix push-to-talk (issue 09): hold = record, release = transcribe locally
    // then refine the text through the OpenAI-compatible endpoint (German
    // cleanup) before injecting. Mirrors OnEnglishTrigger so the two triggers
    // never share record state.
    private void OnFixTrigger(KeyEdge edge)
    {
        if (edge == KeyEdge.Down)
        {
            if (!TryStartRecording())
            {
                return;   // no mic — notice already shown, app keeps running
            }

            _trayIcon.Text = $"{AppInfo.Name} - recording (fix)";
            EnterRecording();   // "listening" pill + red tray + optional start beep
            return;
        }

        _trayIcon.Text = AppInfo.Name;
        if (!_recorder.IsRecording)
        {
            ReturnToIdle();   // key-up without a matching down — make sure we rest
            return;
        }

        var audio = _recorder.Stop();
        PlaySound(start: false);   // optional stop beep at release
        // Read the editable Fix prompt fresh per dictation (issue 35) so a Settings
        // edit takes effect without a restart; blank falls back to default.
        var fixPrompt = ModePrompts.For(RefinedMode.Fix, _settings.Load());
        // Issue 39: Shift layers an English translation on top — Fix becomes cleaned-up
        // English. Fix always has a prompt, so the composed result is never null.
        var prompt = TranslationModifier.Compose(fixPrompt, _alsoTranslate)!;
        var label = _alsoTranslate ? StatusLabel.AlsoEnglish(StatusLabel.Fixing) : StatusLabel.Fixing;
        _ = RefineAndInjectAsync(audio, prompt, label);   // fire-and-forget; outcome surfaces as a balloon only when notable
    }

    // Shared refined-dictation path: transcribe locally, then refine the text
    // through the OpenAI-compatible endpoint with the given <paramref name="systemPrompt"/>
    // before injecting. Used by every LLM mode (Fix, English, Bullets, Custom) —
    // only the prompt differs. Only text crosses the network, never audio.
    private async Task RefineAndInjectAsync(Stream audio, string systemPrompt, string refiningLabel)
    {
        // Starts on the UI thread (the hook delivers events there), so every
        // await resumes on it — balloon calls below are safe.
        // Name the transcription that's about to run: local whisper.cpp vs the
        // cloud API, per the configured source (Pur is the always-local path and
        // goes through DictateAsync, not here).
        var transcribingLocally = _settings.Load().Transcription == TranscriptionMode.Local;
        EnterProcessing(StatusLabel.Transcribing(transcribingLocally));
        try
        {
            // Refined modes already cross the network for the LLM, so the
            // configured transcription source applies: Local → whisper.cpp,
            // Online → the OpenAI Whisper API (issue 12). Resolved per dictation,
            // so a source change in Settings takes effect without a restart.
            var transcriber = await ResolveTranscriberAsync(zeroNetwork: false);

            // Build the refiner from current settings each time so a base
            // URL/model/provider/key change takes effect without an app restart. A
            // missing key still yields a refiner — the pipeline falls back to raw
            // text on the resulting auth failure, so the mode at least inserts the
            // transcript. RefinerAuth gates the key by provider (issue 17): OpenAI
            // sends the stored key, a Local/Ollama endpoint sends none while the key
            // stays stored (it's never deleted on a provider switch).
            var config = _settings.Load();
            var apiKey = RefinerAuth.KeyToSend(config.RefinementProvider, _settings.LoadApiKey());
            var refiner = new OpenAiCompatibleRefiner(
                _httpClient, config.RefinementBaseUrl, config.RefinementModel, apiKey);

            // Refined mode: the delegate runs the given prompt over the transcript
            // between transcription and injection. Only text crosses the network —
            // never audio. On an unreachable endpoint the pipeline injects the raw
            // transcript and reports RefinedOffline (handled below).
            var pipeline = new DictationPipeline(
                new OffloadedTranscriber(transcriber),
                _textInjector,
                refine: (text, ct) =>
                {
                    // Transcription is done; the pill now names the refine step it's
                    // actually running (fixing / bulleting / translating / refining).
                    _overlay.UpdateActive(refiningLabel, OverlayState.Transcribing);
                    return refiner.RefineAsync(text, systemPrompt, ct);
                },
                onResult: _recentDictations.Add,   // issue 26: recoverable history
                transcribeFallback: BuildLocalFallback());   // issue 30: Online → local when offline

            var outcome = await pipeline.RunAsync(audio);

            // Same single fail-soft path as DictateAsync — the refined modes add
            // RefinedOffline and InjectionBlocked, both handled by the mapping.
            Notify(outcome);
        }
        catch (Exception ex)
        {
            // Provisioning failure (e.g. blocked model download) — fail-soft, and
            // point at the manual install for the selected model (issue 22).
            _notifier.Notify(ProvisioningFailureNotice(ex.Message), NoticeLevel.Error);
        }
        finally
        {
            ReturnToIdle();   // hide the pill + tray back to idle once text is in (or failed)
            await audio.DisposeAsync();
        }
    }

    // Swap the tray icon to the colour for this state (idle grey / recording red /
    // processing amber). The icons are pre-built from Core's TrayPalette so this is
    // a cheap reference swap, not a redraw.
    private void SetTrayState(TrayState state) => _trayIcon.Icon = _trayIcons.For(state);

    // Enter the recording state: pill shows "listening", tray turns red, optional
    // start beep. Called from each trigger's Down branch after recording starts.
    private void EnterRecording()
    {
        _overlay.ShowActive(StatusLabel.Listening, OverlayState.Listening);
        SetTrayState(TrayState.Recording);
        PlaySound(start: true);
    }

    // Enter the processing state: the pill names the transcription that's running
    // (Core's StatusLabel decides "transcribing" vs "transcribing locally"), tray
    // turns amber. Called at the start of the async transcribe/refine path after
    // the take stops; the refine step later updates the label to its own verb.
    private void EnterProcessing(string transcribingLabel)
    {
        _overlay.ShowActive(transcribingLabel, OverlayState.Transcribing);
        SetTrayState(TrayState.Processing);
    }

    // Return to rest: hide the pill, tray back to idle grey, optional stop beep.
    // Called from the async methods' finally and when a flex-slot tap aborts.
    private void ReturnToIdle()
    {
        _overlay.Hide();
        SetTrayState(TrayState.Idle);
    }

    // Optional, off by default (config.SoundEnabled): a short system sound on
    // record start/stop. Meeting-friendly silence is the default (design §9).
    private void PlaySound(bool start)
    {
        if (!_soundEnabled)
        {
            return;
        }

        if (start)
        {
            System.Media.SystemSounds.Beep.Play();
        }
        else
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
    }

    // Maps a pipeline outcome to its notice (Core's DictationNotices decides the
    // text and level) and surfaces it through the one notifier. Injected yields
    // null — a successful dictation says nothing.
    private void Notify(DictationOutcome outcome)
    {
        var notice = DictationNotices.For(outcome);
        if (notice is not null)
        {
            _notifier.Notify(notice.Message, notice.Level);
        }
    }

    /// <summary>
    /// Adapts the provisioned <see cref="LocalWhisper"/> to <see cref="ITranscriber"/>
    /// while moving the CPU-bound decode off the UI thread, so the pipeline stays
    /// agnostic of threading and the tray stays responsive during transcription.
    /// </summary>
    private sealed class OffloadedTranscriber(ITranscriber inner) : ITranscriber
    {
        public Task<string> TranscribeAsync(Stream wavAudio, CancellationToken ct = default)
            => Task.Run(() => inner.TranscribeAsync(wavAudio, ct), ct);
    }

    // Where the OpenAI Whisper API lives. Transcription always targets the OpenAI
    // cloud (unlike refinement, whose base URL is user-configurable per provider):
    // the online option exists for lower latency, not for arbitrary endpoints.
    private const string OpenAiTranscriptionBaseUrl = "https://api.openai.com/v1";

    // Resolve this dictation's transcriber (issue 12). Core's TranscriberResolver
    // owns the decision: the configured source picks local whisper.cpp or the
    // OpenAI Whisper API — except zero-network (verbatim) dictation, which always
    // stays local. Factories keep the loser free: with Online selected the local
    // model is never provisioned/downloaded. The online client reuses the one
    // pooled HttpClient and reads the DPAPI key fresh, so a key change applies
    // to the next dictation without a restart.
    private Task<ITranscriber> ResolveTranscriberAsync(bool zeroNetwork) =>
        TranscriberResolver.ResolveAsync(
            _settings.Load().Transcription,
            zeroNetwork,
            local: async () => new OffloadedTranscriber(await _transcriber.GetAsync()),
            online: () => new OpenAiWhisper(
                _httpClient, OpenAiTranscriptionBaseUrl, _settings.LoadApiKey() ?? ""));

    // Offline fail-soft for Online transcription (issue 30). If the network call
    // fails mid-dictation, the pipeline retries through this delegate so the
    // dictation still lands instead of being lost — mirroring the refinement
    // RefinedOffline fail-soft. Deliberately uses *any already-installed* model
    // (never a download): an Online user may have a model configured that was
    // never fetched (e.g. large-v3-turbo with only small on disk), and downloading
    // it offline is impossible. Returns null when no model is installed at all,
    // in which case the pipeline stays fail-soft and reports TranscriptionFailed.
    // A fresh LocalWhisper per fallback is fine on this rare offline path.
    private Func<Stream, CancellationToken, Task<string>>? BuildLocalFallback()
    {
        var provisioner = new ModelProvisioner(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            new GgmlModelDownloader());
        var modelPath = provisioner.FindInstalledModelPath(_settings.Load().WhisperModel);
        if (modelPath is null)
        {
            return null;
        }

        return async (wav, ct) =>
        {
            // Offloaded like the primary so the local decode never blocks the UI
            // thread; disposed once the one-off offline transcription is done.
            using var local = new LocalWhisper(modelPath);
            return await new OffloadedTranscriber(local).TranscribeAsync(wav, ct);
        };
    }

    private async Task<LocalWhisper> ProvisionTranscriberAsync()
    {
        var provisioner = new ModelProvisioner(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            new GgmlModelDownloader());

        // Honor the model the user selected in settings (issue 18) — not a
        // hardcoded default. The choice is read here, lazily, so a Settings change
        // takes effect on the next launch (the AsyncLazy provisions once). Loading
        // tolerates a legacy "base" size (now removed): BlurtConfig just carries
        // whatever was persisted, and the download guidance below is derived from it.
        var model = _settings.Load().WhisperModel;

        // First run only: announce the multi-hundred-MB download so the long
        // wait before the first transcript doesn't look like a hang. The filename
        // and link come from the selected model, so a manual install matches.
        if (!provisioner.IsModelPresent(model))
        {
            _notifier.Notify(
                $"Downloading the {model.Size} Whisper model ({model.FileName})… " +
                ManualInstallHint(model, provisioner),
                NoticeLevel.Info);
        }

        var modelPath = await provisioner.EnsureModelAsync(model);
        return new LocalWhisper(modelPath);
    }

    // The per-selection manual-install guidance (issue 18/22): the exact filename,
    // the working resolve link, and the target folder, all derived from the selected
    // model — so a blocked user (corporate proxy) can install the matching file by
    // hand and it's the very file the runtime loads. Shared by the first-run
    // "Downloading…" notice and the provisioning-failure notice so the two never drift.
    private static string ManualInstallHint(WhisperModel model, ModelProvisioner provisioner) =>
        $"If the download is blocked, place {model.FileName} from {model.DownloadUrl} " +
        $"into {provisioner.ModelsDirectory}.";

    // Builds the provisioning-failure notice (issue 22): the generic error message
    // plus the manual-install guidance for the currently-selected model, so a user
    // whose download was blocked is told exactly which file, link, and folder to use.
    // Reads the selection fresh and re-derives the provisioner the same way
    // ProvisionTranscriberAsync does, so the folder shown is exactly where Blurt loads.
    private string ProvisioningFailureNotice(string message)
    {
        var provisioner = new ModelProvisioner(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            new GgmlModelDownloader());
        var model = _settings.Load().WhisperModel;
        return $"Dictation unavailable: {message}  {ManualInstallHint(model, provisioner)}";
    }

    // Run the first-run wizard (issue 15) modally before the tray takes over. Built
    // with the same SettingsStore (DPAPI key path) and a ModelProvisioner over the
    // real ggml downloader as the transcriber uses — so a model fetched here is the
    // very file the first dictation picks up. Shown with ShowDialog so start-up
    // blocks until the user finishes; any failure inside is contained by the
    // wizard's own fail-soft handling, but guard the show itself so a broken wizard
    // can never stop the app from reaching the tray.
    private void RunOnboarding()
    {
        try
        {
            var provisioner = new ModelProvisioner(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                new GgmlModelDownloader());
            var window = new OnboardingWindow(_settings, provisioner);
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            // Never let onboarding block launch: fall through to the tray. The flow
            // simply re-offers next start (OnboardingCompleted stays false).
            System.Diagnostics.Debug.WriteLine($"Onboarding skipped: {ex}");
        }
    }

    // Play the first-run teaching tutorial (issue 40), modally on this STA thread
    // like onboarding. No dependencies — the cards/copy are pure Core; the window
    // just animates them. Guarded so a broken tutorial can never block launch or
    // the tray; it's a teaching aid, not a gate.
    private void RunTutorial()
    {
        try
        {
            new TutorialWindow().ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tutorial skipped: {ex}");
        }
    }

    // Fill the "Recent dictations" submenu from the ring buffer (issue 26).
    // Newest first, one truncated single-line preview per entry; clicking copies
    // the full text back to the clipboard — the safe recovery default (the user
    // pastes it where they actually want it, instead of a blind re-inject into
    // whatever happens to have focus now).
    private void PopulateRecentDictations(ToolStripMenuItem recentMenu)
    {
        recentMenu.DropDownItems.Clear();

        if (_recentDictations.Items.Count == 0)
        {
            recentMenu.DropDownItems.Add(new ToolStripMenuItem("(no dictations yet)")
            {
                Enabled = false,
            });
            return;
        }

        foreach (var text in _recentDictations.Items)
        {
            var captured = text;   // each click copies its own entry
            recentMenu.DropDownItems.Add(new ToolStripMenuItem(
                RecentDictations.Preview(captured),
                image: null,
                (_, _) => CopyToClipboard(captured)));
        }
    }

    // Fail-soft clipboard write (the clipboard can be transiently locked by
    // another process): a failed copy is a notice, never a crash.
    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            _notifier.Notify("Couldn't copy to the clipboard — try again.", NoticeLevel.Warning);
        }
    }

    // Open the settings window (issue 14). Single instance: if one is already open,
    // bring it to the front instead of stacking a duplicate. The WPF window is shown
    // non-modally from the WinForms STA thread it shares with the rest of the UI —
    // no second System.Windows.Application is created (the overlay already proved
    // WPF and WinForms coexist on this one thread). On a successful save the runtime
    // is re-wired in ApplySettings via the window's Closed callback.
    private void OpenSettings()
    {
        if (_settingsWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        var window = new SettingsWindow(_settings);
        _settingsWindow = window;

        // Issue 20: a modeless WPF window hosted in this WinForms message loop doesn't
        // get keyboard input routed to it unless we ask WinForms-WPF interop to pump
        // its keys. Without this the text fields swallow typed characters (existing
        // text can be deleted but nothing new entered). Keep the window modeless — the
        // tray must stay responsive and the single-instance Activate() path must work —
        // so this is the correct fix rather than switching to ShowDialog().
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(window);

        // Issue 20: suspend the global trigger hook while Settings is open so the
        // trigger characters , . - (and AltGr chords) reach the fields instead of
        // being swallowed / firing a dictation behind the window. Restored on close —
        // but a save re-creates the hook (ApplySettings), which comes back enabled, so
        // only resume the still-live hook when no save replaced it.
        var hookOnOpen = _keyboardHook;
        hookOnOpen.Suspend();

        window.Closed += (_, _) =>
        {
            // Apply only on a genuine save. The window is modeless (Show()), so it
            // can't use DialogResult — SavedConfig is set only by OnSave, so it is
            // the save signal. ApplySettings disposes this hook and installs a fresh,
            // already-enabled one from the new bindings.
            if (window.SavedConfig is { } saved)
            {
                ApplySettings(saved);
            }
            else if (ReferenceEquals(_keyboardHook, hookOnOpen))
            {
                // No save: the same hook is still installed — re-enable trigger handling.
                hookOnOpen.Resume();
            }

            _settingsWindow = null;
        };
        window.Show();
    }

    // Apply a freshly-saved config to the running app. Hotkeys, overlay anchor, and
    // sound take effect live; what cannot (transcription source, the selected Whisper
    // model) is persisted now and picked up on the next launch.
    private void ApplySettings(BlurtConfig config)
    {
        // Hotkeys: re-create the hook from the new bindings so remapped chords fire
        // and the old ones no longer do. Dispose the old hook (uninstalls it) first.
        _keyboardHook.Dispose();
        _keyboardHook = InstallHook(config);

        // Overlay anchor: the controller captures the anchor at construction, so swap
        // in a fresh one (closing the old window) for the change to take effect.
        _overlay.Dispose();
        _overlay = new OverlayController(config.OverlayAnchor);

        // Sound flag: read live on each PlaySound, so just update the field.
        _soundEnabled = config.SoundEnabled;

        // Transcription source and the selected local model feed the transcriber,
        // which is provisioned once lazily (reading the configured model in
        // ProvisionTranscriberAsync) — those changes apply on the next launch.
    }

    private void ExitApp()
    {
        _settingsWindow?.Close();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyboardHook.Dispose();   // uninstall the hook so nothing leaks on exit
            _recorder.Dispose();       // releases the capture device if mid-recording
            _httpClient.Dispose();     // close the refiner's pooled connections
            _overlay.Dispose();        // close the WPF overlay window (issue 06)
            _trayIcon.Dispose();
            _trayIcons.Dispose();      // free the generated tray icon GDI handles (issue 06)
        }

        base.Dispose(disposing);
    }
}
