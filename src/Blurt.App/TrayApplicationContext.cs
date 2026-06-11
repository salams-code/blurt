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
    private readonly KeyboardHook _keyboardHook;
    private readonly TextInjector _textInjector;
    private readonly AudioRecorder _recorder = new();

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

        _trayIcon = new NotifyIcon
        {
            // Placeholder icon until the idle/recording/processing icons land (issue 06).
            Icon = SystemIcons.Application,
            Text = AppInfo.Name,
            Visible = true,
            ContextMenuStrip = menu,
        };

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

    // Visible feedback that the hook fired and the keystroke was swallowed.
    // Real status icons/overlay come later (issues 06, and overlay work).
    private void OnTriggerObserved(TriggerEvent trigger)
    {
        // Issue 04 slice: only the English trigger does real push-to-talk
        // dictation; Fix and FlexSlot keep the placeholder feedback below.
        if (trigger.Kind == TriggerKind.English)
        {
            OnEnglishTrigger(trigger.Edge);
            return;
        }

        // Icon swap + tooltip only — a balloon per keypress floods the Windows
        // notification center. The real status display is issue 06's overlay.
        if (trigger.Edge == KeyEdge.Down)
        {
            _trayIcon.Icon = SystemIcons.Information;
            _trayIcon.Text = $"{AppInfo.Name} - {trigger.Kind} (down)";
        }
        else
        {
            _trayIcon.Icon = SystemIcons.Application;
            _trayIcon.Text = AppInfo.Name;

            // Issue 03 manual check: Fix release injects a fixed string. The
            // real dictation pipeline replaces this in issue 05. Fire-and-forget
            // is fine here — the injector resumes on this (STA) UI thread and
            // never throws past the paste seam.
            if (trigger.Kind == TriggerKind.Fix)
            {
                _ = _textInjector.InjectAsync("hello from blurt");
            }
        }
    }

    // Push-to-talk dictation (issue 05): hold = record, release = transcribe
    // locally and inject the verbatim text at the cursor. This is "Pur" mode —
    // no refinement, zero network — driven by the Core DictationPipeline.
    private void OnEnglishTrigger(KeyEdge edge)
    {
        if (edge == KeyEdge.Down)
        {
            _recorder.Start();
            _trayIcon.Icon = SystemIcons.Information;
            _trayIcon.Text = $"{AppInfo.Name} - recording";
            return;
        }

        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = AppInfo.Name;
        if (!_recorder.IsRecording)
        {
            return;   // key-up without a matching down (e.g. held across app start)
        }

        var audio = _recorder.Stop();
        _ = DictateAsync(audio);   // fire-and-forget; outcome surfaces as a balloon only when notable
    }

    private async Task DictateAsync(Stream audio)
    {
        // This method starts on the UI thread (the hook delivers events there),
        // so every await resumes on it — balloon calls below are safe.
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

            // Fail-soft notices: success injects silently; only the cases where
            // nothing landed at the cursor warrant a balloon.
            switch (outcome)
            {
                case DictationOutcome.NothingTranscribed:
                    _trayIcon.ShowBalloonTip(3000, AppInfo.Name, "(no speech detected)", ToolTipIcon.Info);
                    break;
                case DictationOutcome.TranscriptionFailed:
                    _trayIcon.ShowBalloonTip(5000, AppInfo.Name, "Transcription failed.", ToolTipIcon.Error);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Provisioning failure (e.g. blocked model download) — fail-soft.
            _trayIcon.ShowBalloonTip(5000, AppInfo.Name, $"Dictation unavailable: {ex.Message}", ToolTipIcon.Error);
        }
        finally
        {
            await audio.DisposeAsync();
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
            _trayIcon.ShowBalloonTip(
                5000,
                AppInfo.Name,
                $"Downloading Whisper model ({WhisperModel.Default.FileName}, ~460 MB)…",
                ToolTipIcon.Info);
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
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
