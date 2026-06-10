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
    private Task<LocalWhisper>? _transcriber;

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

        if (trigger.Edge == KeyEdge.Down)
        {
            _trayIcon.Icon = SystemIcons.Information;
            _trayIcon.Text = $"{AppInfo.Name} - {trigger.Kind} (down)";
            _trayIcon.ShowBalloonTip(400, AppInfo.Name, $"{trigger.Kind} trigger down", ToolTipIcon.Info);
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

    // Push-to-talk dictation (issue 04): hold = record, release = transcribe
    // locally and surface the raw text in a balloon. Injection comes in 03/05.
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
        _ = TranscribeAndShowAsync(audio);   // fire-and-forget; errors surface as a balloon
    }

    private async Task TranscribeAndShowAsync(Stream audio)
    {
        // This method starts on the UI thread (the hook delivers events there),
        // so every await resumes on it — balloon calls below are safe. The
        // CPU-heavy work runs on the thread pool to keep the tray responsive.
        try
        {
            var transcriber = await GetTranscriberAsync();
            var text = await Task.Run(() => transcriber.TranscribeAsync(audio));
            _trayIcon.ShowBalloonTip(
                5000,
                $"{AppInfo.Name} transcript",
                string.IsNullOrWhiteSpace(text) ? "(no speech detected)" : text,
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            // Fail-soft (design §10): transcription failure is a notice, not a crash.
            _trayIcon.ShowBalloonTip(5000, AppInfo.Name, $"Transcription failed: {ex.Message}", ToolTipIcon.Error);
        }
        finally
        {
            await audio.DisposeAsync();
        }
    }

    private Task<LocalWhisper> GetTranscriberAsync() => _transcriber ??= ProvisionTranscriberAsync();

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
