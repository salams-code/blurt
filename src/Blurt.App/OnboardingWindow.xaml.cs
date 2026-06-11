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

    // Not readonly: the provider step (issue 17) updates the in-progress config
    // (provider + base URL) before Finish() persists it with OnboardingCompleted.
    private BlurtConfig _config;

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
        ShowProviderChoice();
        ShowHotkeys();
        ShowStep(OnboardingStep.Microphone);
    }

    // Reflect any existing provider/base-URL choice so reopening (e.g. onboarding
    // re-offered after a skip) shows the prior selection rather than resetting it.
    private void ShowProviderChoice()
    {
        var local = _config.RefinementProvider == RefinementProvider.LocalOpenAiCompatible;
        LocalProviderRadio.IsChecked = local;
        OpenAiProviderRadio.IsChecked = !local;
        if (local)
            LocalBaseUrlBox.Text = _config.RefinementBaseUrl;
        OnProviderChoiceChanged(this, new RoutedEventArgs());   // sync the sub-cards
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
        // Persist the provider choice when leaving that step (whether via Next or
        // Skip): OpenAI saves a typed key through DPAPI (blank = skip, existing key
        // untouched); Local records the provider + base URL. The stored key is never
        // deleted on a provider switch.
        if (_step == OnboardingStep.ApiKey)
            SaveProviderChoice();

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
    // persisting it here is what stops the wizard from ever showing again. The mic
    // chosen in step 1 is persisted too (issue 16) so dictation records from it,
    // not just the level meter — see SelectedInputDevice.
    private void Finish()
    {
        StopLevelMeter();

        var (mode, name) = SelectedInputDevice();
        _store.Save(_config with
        {
            OnboardingCompleted = true,
            InputDeviceMode = mode,
            InputDeviceName = name,
        });
        DialogResult = true;
        Close();
    }

    // The capture device the user picked in step 1, mapped to the config fields. A
    // real selection pins that device by name (Specific); if no device was found or
    // the box is disabled, fall back to FollowDefault so the recorder uses whatever
    // is default (and a device connected later just works). Issue 16.
    private (InputDeviceMode Mode, string Name) SelectedInputDevice()
    {
        if (MicrophoneBox.IsEnabled && MicrophoneBox.SelectedItem is string name && name.Length > 0)
            return (InputDeviceMode.Specific, name);

        return (InputDeviceMode.FollowDefault, "");
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

    // --- Step 2: provider + API key (DPAPI, skippable) ---------------------

    // Toggle the two sub-cards as the provider radio changes (issue 17): OpenAI
    // shows the key guide; Local shows the endpoint URL. Choosing Local never
    // clears the key entry — switching providers only changes what's sent, not
    // what's stored (RefinerAuth gates the key at refine time).
    private void OnProviderChoiceChanged(object sender, RoutedEventArgs e)
    {
        // Guard: Checked can fire during InitializeComponent before the cards exist.
        if (OpenAiKeyCard is null || LocalEndpointCard is null)
            return;

        var local = LocalProviderRadio.IsChecked == true;
        OpenAiKeyCard.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        LocalEndpointCard.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
    }

    // Persist the provider choice when leaving the step (issue 17). OpenAI: save a
    // typed key (blank = skip, existing key untouched). Local: record the provider
    // and base URL so refinement targets the keyless endpoint — the stored key is
    // never deleted, just not sent (RefinerAuth). Mutates the in-progress _config
    // so Finish() persists the choice alongside OnboardingCompleted.
    private void SaveProviderChoice()
    {
        if (LocalProviderRadio.IsChecked == true)
        {
            var baseUrl = LocalBaseUrlBox.Text.Trim();
            _config = _config with
            {
                RefinementProvider = RefinementProvider.LocalOpenAiCompatible,
                RefinementBaseUrl = string.IsNullOrEmpty(baseUrl) ? _config.RefinementBaseUrl : baseUrl,
            };
            LocalHint.Text = "Local endpoint selected — no key needed.";
            return;
        }

        // OpenAI cloud: keep the provider on OpenAI and save a typed key if any.
        _config = _config with { RefinementProvider = RefinementProvider.OpenAi };

        var key = ApiKeyBox.Password;
        if (string.IsNullOrWhiteSpace(key))
            return;   // skipped — refinement falls back offline, no key stored

        _store.SaveApiKey(key.Trim());   // DPAPI via the store; never plaintext
        ApiKeyHint.Text = "Key saved (encrypted).";
    }

    // --- Step 3: model (present → skip; missing → fail-soft download) -------

    // Check the SELECTED model (issue 18), not a hardcoded small. Whatever the
    // config specifies drives the presence check, the expected filename, and the
    // manual-install guidance — so what onboarding shows matches what the app loads.
    private async Task CheckModelAsync()
    {
        var model = _config.WhisperModel;

        if (_provisioner.IsModelPresent(model))
        {
            // Already downloaded (the common case on this machine) — never re-fetch.
            ModelStatus.Text = $"✓  The {model.Size} model is already installed.";
            ModelProgress.Visibility = Visibility.Collapsed;
            DownloadButton.Visibility = Visibility.Collapsed;
            ModelHint.Text = "Nothing to do — you're ready to dictate.";
            return;
        }

        ModelStatus.Text = $"The {model.Size} model isn't installed yet.";
        DownloadButton.Visibility = Visibility.Visible;
        DownloadButton.IsEnabled = true;
        ModelHint.Text = ManualInstallHint(model);
        await Task.CompletedTask;
    }

    private async void OnDownloadModel(object sender, RoutedEventArgs e)
    {
        var model = _config.WhisperModel;
        DownloadButton.IsEnabled = false;
        ModelProgress.Visibility = Visibility.Visible;
        ModelProgress.IsIndeterminate = true;
        ModelStatus.Text = $"Downloading the {model.Size} model…";
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
            // The manual-install guidance (exact file, link, folder) lets a blocked
            // colleague install the matching file by hand.
            ModelProgress.Visibility = Visibility.Collapsed;
            ModelStatus.Text = "Download failed.";
            DownloadButton.Content = "Retry";
            DownloadButton.Visibility = Visibility.Visible;
            DownloadButton.IsEnabled = true;
            ModelHint.Text = $"{ex.Message}  {ManualInstallHint(model)}";
        }
    }

    // Per-selection manual-install guidance (issue 18): the exact filename, a working
    // resolve link, and the target folder, all derived from the selected model so a
    // hand install matches what Blurt loads. The corporate proxy blocks the in-app
    // download, so this is the path colleagues actually use.
    private string ManualInstallHint(WhisperModel model) =>
        $"If the download is blocked, get {model.FileName} from {model.DownloadUrl} " +
        $"and place it in {_provisioner.ModelsDirectory} — then continue.";

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
