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
    private readonly OverlayController _overlay;
    private readonly TrayIcons _trayIcons = new();
    private readonly bool _soundEnabled;

    // The single fail-soft surface (issue 13): every notice — outcome balloons,
    // provisioning failures, the no-mic warning, flex-slot hints — goes through
    // here instead of calling ShowBalloonTip directly, so they share one
    // non-blocking channel. Tray-only today; the overlay channel (issue 06)
    // plugs into TrayNotifier without touching these call sites.
    private readonly INotifier _notifier;

    private readonly KeyboardHook _keyboardHook;
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
        menu.Items.Add("Exit", image: null, (_, _) => ExitApp());

        // Read the overlay anchor and sound toggle once at start-up; both are
        // stable user preferences (a restart picks up a change, like the hotkeys).
        var config = _settings.Load();
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

        _keyboardHook = new KeyboardHook();
        _keyboardHook.TriggerObserved += OnTriggerObserved;
        _keyboardHook.Install();
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

        // First run only: announce the multi-hundred-MB download so the long
        // wait before the first transcript doesn't look like a hang.
        if (!provisioner.IsModelPresent(WhisperModel.Default))
        {
            _notifier.Notify(
                $"Downloading Whisper model ({WhisperModel.Default.FileName}, ~460 MB)…",
                NoticeLevel.Info);
        }

        var modelPath = await provisioner.EnsureModelAsync(WhisperModel.Default);
        return new LocalWhisper(modelPath);
    }

    private void ExitApp()
    {
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
