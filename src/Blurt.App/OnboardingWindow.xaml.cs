using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Blurt.Core;
using NAudio.Wave;

namespace Blurt.App;

/// <summary>
/// The WPF first-run wizard (issue 15). Walks the four <see cref="OnboardingStep"/>s
/// — microphone + live level, optional OpenAI API key, Whisper model, hotkey review
/// — and on finish persists <see cref="BlurtConfig.OnboardingCompleted"/> = true so
/// the flow never runs again. Pure decisions (whether onboarding is needed, the step
/// order) live in Core; this is the thin, manually-verified UI shell. Shown modally
/// from the WinForms STA thread before the tray goes quiet (see
/// <see cref="TrayApplicationContext"/>); no second System.Windows.Application is
/// created — WPF and WinForms already coexist on this one thread (the overlay proved it).
/// </summary>
internal partial class OnboardingWindow : Window
{
    private readonly SettingsStore _store;
    private readonly ModelProvisioner _provisioner;
    private readonly BlurtConfig _config;

    // The step currently shown. Drives panel visibility and button labels.
    private OnboardingStep _step = OnboardingStep.Microphone;

    // Live mic-level capture for the microphone step. Created when the step is
    // entered or the device changes, and disposed when the step is left — so no
    // device stays open while the user is on a later step.
    private WaveInEvent? _levelMeter;
    private DispatcherTimer? _levelDecay;
    private float _currentPeak;

    public OnboardingWindow(SettingsStore store, ModelProvisioner provisioner)
    {
        InitializeComponent();

        _store = store;
        _provisioner = provisioner;
        _config = store.Load();

        PopulateMicrophones();
        ShowHotkeys();
        ShowStep(OnboardingStep.Microphone);
    }

    // --- Step navigation ----------------------------------------------------

    // The four step panels, in OnboardingStep order, so showing a step is a single
    // lookup rather than a switch repeated per concern.
    private StackPanel PanelFor(OnboardingStep step) => step switch
    {
        OnboardingStep.Microphone => MicrophonePanel,
        OnboardingStep.ApiKey => ApiKeyPanel,
        OnboardingStep.Model => ModelPanel,
        OnboardingStep.Hotkeys => HotkeysPanel,
        _ => MicrophonePanel,
    };

    private void ShowStep(OnboardingStep step)
    {
        // Leaving the microphone step must stop the live capture immediately.
        if (_step == OnboardingStep.Microphone && step != OnboardingStep.Microphone)
            StopLevelMeter();

        _step = step;

        foreach (var s in Enum.GetValues<OnboardingStep>())
            PanelFor(s).Visibility = s == step ? Visibility.Visible : Visibility.Collapsed;

        var index = (int)step;
        var count = Enum.GetValues<OnboardingStep>().Length;
        StepIndicator.Text = $"Step {index + 1} of {count}";

        BackButton.IsEnabled = index > 0;
        var isLast = index == count - 1;
        NextButton.Content = isLast ? "Finish" : "Next";

        // The API-key step is explicitly skippable; the rest advance via "Next".
        SkipButton.Visibility = step == OnboardingStep.ApiKey ? Visibility.Visible : Visibility.Collapsed;

        // Entering a step kicks off its side effects.
        if (step == OnboardingStep.Microphone)
            StartLevelMeter();
        else if (step == OnboardingStep.Model)
            _ = CheckModelAsync();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        var index = (int)_step;
        if (index > 0)
            ShowStep((OnboardingStep)(index - 1));
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        // Persist the API key when leaving that step (whether via Next or Skip):
        // a typed value is saved through DPAPI; blank means "skip", leaving any
        // existing key untouched.
        if (_step == OnboardingStep.ApiKey)
            SaveApiKeyIfTyped();

        var index = (int)_step;
        var count = Enum.GetValues<OnboardingStep>().Length;
        if (index < count - 1)
        {
            ShowStep((OnboardingStep)(index + 1));
            return;
        }

        Finish();
    }

    // Mark onboarding done and close. The flag is the single source of truth, so
    // persisting it here is what stops the wizard from ever showing again.
    private void Finish()
    {
        StopLevelMeter();
        _store.Save(_config with { OnboardingCompleted = true });
        DialogResult = true;
        Close();
    }

    // --- Step 1: microphone + live level -----------------------------------

    private void PopulateMicrophones()
    {
        var devices = new List<string>();
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            devices.Add(WaveInEvent.GetCapabilities(i).ProductName);

        if (devices.Count == 0)
        {
            MicrophoneBox.ItemsSource = new[] { "(no input device found)" };
            MicrophoneBox.SelectedIndex = 0;
            MicrophoneBox.IsEnabled = false;
            MicStatus.Text = "No microphone detected. You can still finish setup and connect one later.";
            return;
        }

        MicrophoneBox.ItemsSource = devices;
        MicrophoneBox.SelectedIndex = 0;
    }

    private void OnMicrophoneChanged(object sender, SelectionChangedEventArgs e)
    {
        // Restart capture on the newly chosen device, but only while this step is
        // actually showing (the initial SelectedIndex assignment fires this too).
        if (_step == OnboardingStep.Microphone && MicrophoneBox.IsEnabled)
            StartLevelMeter();
    }

    private void StartLevelMeter()
    {
        StopLevelMeter();

        if (!MicrophoneBox.IsEnabled || MicrophoneBox.SelectedIndex < 0)
            return;

        try
        {
            _levelMeter = new WaveInEvent
            {
                DeviceNumber = MicrophoneBox.SelectedIndex,
                WaveFormat = new WaveFormat(rate: 16000, bits: 16, channels: 1),
            };
            _levelMeter.DataAvailable += OnLevelData;
            _levelMeter.StartRecording();

            // Smoothly relax the bar between buffers so it falls back to zero in
            // silence instead of freezing at the last peak.
            _levelDecay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _levelDecay.Tick += OnLevelDecay;
            _levelDecay.Start();

            MicStatus.Text = "Speak now — the bar should move as you talk.";
        }
        catch (Exception ex)
        {
            // Fail-soft: a busy or permission-denied device must not crash the wizard.
            StopLevelMeter();
            MicStatus.Text = $"Couldn't open this microphone ({ex.Message}). Try another, or finish and fix it later.";
        }
    }

    // Compute the buffer's peak sample (0..1) off the captured 16-bit PCM. The bar
    // is updated on the decay timer (UI thread); DataAvailable fires on NAudio's
    // own thread, so we only stash the value here.
    private void OnLevelData(object? sender, WaveInEventArgs e)
    {
        var peak = 0f;
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
            var magnitude = Math.Abs(sample);
            if (magnitude > peak)
                peak = magnitude;
        }

        // Keep the loudest recent peak; the decay timer eases it back down.
        if (peak > _currentPeak)
            _currentPeak = peak;
    }

    private void OnLevelDecay(object? sender, EventArgs e)
    {
        // Bar width tracks the current peak across the meter's pixel width; the
        // outer track is the LevelBar's parent Border.
        if (LevelBar.Parent is FrameworkElement track && track.ActualWidth > 0)
            LevelBar.Width = Math.Clamp(_currentPeak, 0f, 1f) * track.ActualWidth;

        // Relax the peak so silence returns the bar to zero.
        _currentPeak *= 0.8f;
    }

    private void StopLevelMeter()
    {
        if (_levelDecay is not null)
        {
            _levelDecay.Stop();
            _levelDecay.Tick -= OnLevelDecay;
            _levelDecay = null;
        }

        if (_levelMeter is not null)
        {
            _levelMeter.DataAvailable -= OnLevelData;
            try
            {
                _levelMeter.StopRecording();
            }
            catch
            {
                // Already stopped / device gone — nothing to clean up beyond Dispose.
            }
            _levelMeter.Dispose();
            _levelMeter = null;
        }

        _currentPeak = 0f;
        LevelBar.Width = 0;
    }

    // --- Step 2: API key (DPAPI, skippable) --------------------------------

    private void SaveApiKeyIfTyped()
    {
        var key = ApiKeyBox.Password;
        if (string.IsNullOrWhiteSpace(key))
            return;   // skipped — refinement falls back offline, no key stored

        _store.SaveApiKey(key.Trim());   // DPAPI via the store; never plaintext
        ApiKeyHint.Text = "Key saved (encrypted).";
    }

    // --- Step 3: model (present → skip; missing → fail-soft download) -------

    private async Task CheckModelAsync()
    {
        var model = WhisperModel.Default;

        if (_provisioner.IsModelPresent(model))
        {
            // Already downloaded (the common case on this machine) — never re-fetch.
            ModelStatus.Text = $"✓  The {model.Size} model is already installed.";
            ModelProgress.Visibility = Visibility.Collapsed;
            DownloadButton.Visibility = Visibility.Collapsed;
            ModelHint.Text = "Nothing to do — you're ready to dictate.";
            return;
        }

        ModelStatus.Text = $"The {model.Size} model (~460 MB) isn't installed yet.";
        DownloadButton.Visibility = Visibility.Visible;
        DownloadButton.IsEnabled = true;
        ModelHint.Text = "Downloading needs internet access. You can also continue and add it later.";
        await Task.CompletedTask;
    }

    private async void OnDownloadModel(object sender, RoutedEventArgs e)
    {
        var model = WhisperModel.Default;
        DownloadButton.IsEnabled = false;
        ModelProgress.Visibility = Visibility.Visible;
        ModelProgress.IsIndeterminate = true;
        ModelStatus.Text = $"Downloading the {model.Size} model (~460 MB)…";
        ModelHint.Text = "This can take a few minutes on first run.";

        try
        {
            await _provisioner.EnsureModelAsync(model);
            ModelProgress.IsIndeterminate = false;
            ModelProgress.Value = 1;
            ModelStatus.Text = $"✓  The {model.Size} model is installed.";
            DownloadButton.Visibility = Visibility.Collapsed;
            ModelHint.Text = "Done — you're ready to dictate.";
        }
        catch (Exception ex)
        {
            // Fail-soft: a blocked proxy / no connection must not trap the user.
            // They can retry here or continue; the runtime re-attempts the download
            // on the first dictation anyway (AsyncLazy forgets failed attempts).
            ModelProgress.Visibility = Visibility.Collapsed;
            ModelStatus.Text = "Download failed.";
            DownloadButton.Content = "Retry";
            DownloadButton.Visibility = Visibility.Visible;
            DownloadButton.IsEnabled = true;
            ModelHint.Text =
                $"{ex.Message}  You can retry, add the model manually later, or continue — " +
                "Blurt will try again on your first dictation.";
        }
    }

    // --- Step 4: hotkeys ----------------------------------------------------

    private void ShowHotkeys()
    {
        HotkeyFix.Text = ChordFor(TriggerKind.Fix);
        HotkeyEnglish.Text = ChordFor(TriggerKind.English);
        HotkeyFlex.Text = ChordFor(TriggerKind.FlexSlot);
    }

    // Prefer the stored chord text; if it's missing/garbage, fall back to the
    // canonical render of the resolved VK so the user never sees a blank.
    private string ChordFor(TriggerKind trigger)
    {
        if (_config.HotkeyBindings.TryGetValue(trigger, out var chord)
            && HotkeyBinding.TryParse(chord, out var vk))
        {
            return HotkeyBinding.Format(vk);
        }

        return _config.HotkeyBindings.TryGetValue(trigger, out var raw) ? raw : "";
    }

    protected override void OnClosed(EventArgs e)
    {
        StopLevelMeter();   // belt-and-braces: never leave the capture device open
        base.OnClosed(e);
    }
}
