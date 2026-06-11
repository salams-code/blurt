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

    // Flex-slot state (issue 07). The classifier turns the key's hold duration
    // into tap-vs-hold; the cycle rotates Pur → Bullets → Custom on each tap.
    // Both are pure Core logic; this glue only measures time and dispatches.
    private readonly TapHoldClassifier _tapHoldClassifier = new();
    private readonly FlexSlotCycle _flexSlotCycle = new();

    // When the Flex-slot key went down, so its release can be classified as a
    // tap (cycle the mode) or a hold (dictate). Null between presses.
    private long? _flexSlotDownTicks;

    // Created lazily on the first dictation so the model download (first run
    // only) never happens before the user actually asks for a transcription.
    // AsyncLazy forgets failed attempts: a failed download (e.g. blocked
    // network) is retried on the next dictation instead of poisoning every
    // attempt until app restart.
    private readonly AsyncLazy<LocalWhisper> _transcriber;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", image: null, (_, _) => OpenSettings());
        menu.Items.Add("Exit", image: null, (_, _) => ExitApp());

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

    // Fail-soft microphone start (design §10, mode 1): opening the default
    // capture device throws (NAudio) when there is no microphone or permission
    // is denied. Catch it, surface a notice through the notifier, and return
    // false so the down-handler simply doesn't enter recording — the app keeps
    // running instead of crashing on a missing device.
    private bool TryStartRecording()
    {
        try
        {
            _recorder.Start();
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
        _ = RefineAndInjectAsync(audio, RefinementPrompts.English);   // fire-and-forget; outcome surfaces as a balloon only when notable
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
            if (_recorder.IsRecording)
            {
                _recorder.Stop().Dispose();
            }
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
            // advance the mode, surfacing the new one in the tray. No transcription,
            // so the overlay goes straight back to idle (no "transcribing" pill).
            _recorder.Stop().Dispose();
            ReturnToIdle();
            var mode = _flexSlotCycle.Cycle();
            _trayIcon.Text = $"{AppInfo.Name} - {mode}";
            _notifier.Notify($"Flex slot: {mode}", NoticeLevel.Info);
            return;
        }

        // Hold: dictate with the current mode. FlexSlotPrompts resolves the system
        // prompt for the mode: Pur and an unset Custom yield null ("no refiner"),
        // so those go through verbatim DictateAsync (zero network); Bullets and a
        // configured Custom carry a prompt and run RefineAndInjectAsync.
        var audio = _recorder.Stop();
        var currentMode = _flexSlotCycle.Current;
        var prompt = FlexSlotPrompts.For(currentMode, _settings.Load());

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
            _ = RefineAndInjectAsync(audio, prompt);   // Bullets / configured Custom
        }
    }

    private async Task DictateAsync(Stream audio)
    {
        // This method starts on the UI thread (the hook delivers events there),
        // so every await resumes on it — balloon calls below are safe.
        EnterProcessing();   // "transcribing" pill + amber tray for the decode
        try
        {
            // Provisioning (first-run download) can fail on a blocked network;
            // keep that a soft notice rather than letting it reach the pipeline.
            var transcriber = await _transcriber.GetAsync();

            // Pur mode: no refinement delegate, so verbatim Whisper output is
            // injected at the cursor. The transcriber adapter pushes the
            // CPU-heavy decode onto the thread pool to keep the tray responsive.
            var pipeline = new DictationPipeline(
                new OffloadedTranscriber(transcriber),
                _textInjector);

            var outcome = await pipeline.RunAsync(audio);

            // Single fail-soft path: DictationNotices decides what (if anything)
            // to say for this outcome — Injected is silent, every other case maps
            // to a notice surfaced through the one notifier.
            Notify(outcome);
        }
        catch (Exception ex)
        {
            // Provisioning failure (e.g. blocked model download) — fail-soft.
            _notifier.Notify($"Dictation unavailable: {ex.Message}", NoticeLevel.Error);
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
        _ = RefineAndInjectAsync(audio, RefinementPrompts.Fix);   // fire-and-forget; outcome surfaces as a balloon only when notable
    }

    // Shared refined-dictation path: transcribe locally, then refine the text
    // through the OpenAI-compatible endpoint with the given <paramref name="systemPrompt"/>
    // before injecting. Used by every LLM mode (Fix, English, Bullets, Custom) —
    // only the prompt differs. Only text crosses the network, never audio.
    private async Task RefineAndInjectAsync(Stream audio, string systemPrompt)
    {
        // Starts on the UI thread (the hook delivers events there), so every
        // await resumes on it — balloon calls below are safe.
        EnterProcessing();   // "transcribing" pill + amber tray while we transcribe+refine
        try
        {
            var transcriber = await _transcriber.GetAsync();

            // Build the refiner from current settings each time so a base
            // URL/model/key change takes effect without an app restart. A missing
            // key still yields a refiner — the pipeline falls back to raw text on
            // the resulting auth failure, so the mode at least inserts the transcript.
            var config = _settings.Load();
            var apiKey = _settings.LoadApiKey() ?? string.Empty;
            var refiner = new OpenAiCompatibleRefiner(
                _httpClient, config.RefinementBaseUrl, config.RefinementModel, apiKey);

            // Refined mode: the delegate runs the given prompt over the transcript
            // between transcription and injection. Only text crosses the network —
            // never audio. On an unreachable endpoint the pipeline injects the raw
            // transcript and reports RefinedOffline (handled below).
            var pipeline = new DictationPipeline(
                new OffloadedTranscriber(transcriber),
                _textInjector,
                refine: (text, ct) => refiner.RefineAsync(text, systemPrompt, ct));

            var outcome = await pipeline.RunAsync(audio);

            // Same single fail-soft path as DictateAsync — the refined modes add
            // RefinedOffline and InjectionBlocked, both handled by the mapping.
            Notify(outcome);
        }
        catch (Exception ex)
        {
            // Provisioning failure (e.g. blocked model download) — fail-soft.
            _notifier.Notify($"Dictation unavailable: {ex.Message}", NoticeLevel.Error);
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
        _overlay.Show(OverlayState.Listening);
        SetTrayState(TrayState.Recording);
        PlaySound(start: true);
    }

    // Enter the processing state: pill shows "transcribing", tray turns amber.
    // Called at the start of the async transcribe/refine path after the take stops.
    private void EnterProcessing()
    {
        _overlay.Show(OverlayState.Transcribing);
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
                $"Downloading Whisper model ({model.FileName})… If this is blocked, " +
                $"place the file from {model.DownloadUrl} into {provisioner.ModelsDirectory}.",
                NoticeLevel.Info);
        }

        var modelPath = await provisioner.EnsureModelAsync(model);
        return new LocalWhisper(modelPath);
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
        window.Closed += (_, _) =>
        {
            // Apply only on a genuine save (DialogResult true + a captured config).
            if (window.DialogResult == true && window.SavedConfig is { } saved)
            {
                ApplySettings(saved);
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
