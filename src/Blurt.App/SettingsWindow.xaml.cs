using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Blurt.Core;
using NAudio.Wave;

namespace Blurt.App;

/// <summary>
/// The WPF settings window over <see cref="SettingsStore"/>. It loads the current
/// config into its fields, validates edits through Core's pure
/// <see cref="SettingsValidation"/> before persisting, and writes the API key
/// through the store's DPAPI path — never showing or storing it in plaintext. All
/// runtime re-wiring (re-installing the hook with the new hotkeys, updating the
/// overlay anchor and sound flag) is the caller's job: this window only persists
/// and reports the saved <see cref="BlurtConfig"/> via <see cref="SavedConfig"/>.
///
/// Pure decision logic lives in Core; this is the thin, manually-verified UI shell.
/// </summary>
internal partial class SettingsWindow : Window
{
    // Placeholder shown in the password box when a key is already stored, so the
    // real key never reaches the UI. Leaving it untouched means "keep the key".
    private const string ApiKeyPlaceholder = "(unchanged)";

    private readonly SettingsStore _store;
    private readonly BlurtConfig _original;
    private readonly bool _hadApiKey;

    /// <summary>
    /// The config that was persisted, set once <see cref="OnSave"/> succeeds (the
    /// window then closes with <c>DialogResult == true</c>). Null if cancelled.
    /// </summary>
    public BlurtConfig? SavedConfig { get; private set; }

    public SettingsWindow(SettingsStore store)
    {
        InitializeComponent();

        _store = store;
        _original = store.Load();
        _hadApiKey = store.LoadApiKey() is { Length: > 0 };

        PopulateChoices();
        LoadFromConfig(_original);
    }

    // One microphone-combo entry. A null DeviceName is the "(Windows default)" item
    // (FollowDefault); a non-null name is a specific enumerated device. ToString is
    // what the ComboBox renders. Issue 16.
    private sealed record MicrophoneChoice(string Label, string? DeviceName)
    {
        public override string ToString() => Label;
    }

    // Fill the fixed-choice combo boxes once. Enum values back the items directly so
    // mapping back on save is a straight cast.
    private void PopulateChoices()
    {
        TranscriptionSourceBox.ItemsSource = new[] { TranscriptionMode.Local, TranscriptionMode.Online };
        OverlayAnchorBox.ItemsSource = new[] { OverlayAnchor.MousePointer, OverlayAnchor.BottomCenter };
        // The two local models offered (issue 18): the small default and the
        // higher-quality large-v3-turbo. The combo renders each model's Size.
        ModelSizeBox.ItemsSource = ModelChoices;
        ModelSizeBox.DisplayMemberPath = nameof(WhisperModel.Size);

        // Refinement provider (issue 17): friendly labels over the enum so the local
        // option reads clearly. SelectedValue carries the enum back on save.
        RefinementProviderBox.DisplayMemberPath = nameof(ProviderChoice.Label);
        RefinementProviderBox.SelectedValuePath = nameof(ProviderChoice.Value);
        RefinementProviderBox.ItemsSource = new[]
        {
            new ProviderChoice("OpenAI", RefinementProvider.OpenAi),
            new ProviderChoice("Local (Ollama, OpenAI-compatible)", RefinementProvider.LocalOpenAiCompatible),
        };

        PopulateMicrophones();
    }

    // The local models the window offers, in display order. The download guidance
    // and the load-back match (LoadFromConfig) both derive from this one list, so
    // adding a model is a single edit and a stored, now-unknown size (e.g. the
    // removed "base") simply falls back to the first entry.
    private static readonly WhisperModel[] ModelChoices = [WhisperModel.Default, WhisperModel.Turbo];

    // Microphone list (issue 16): "(Windows default)" → FollowDefault, plus every
    // enumerated input device by its WaveInCapabilities.ProductName → Specific. The
    // names are exactly what the Core resolver and AudioRecorder match against.
    private void PopulateMicrophones()
    {
        var choices = new List<MicrophoneChoice> { new("(Windows default)", DeviceName: null) };
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var name = WaveInEvent.GetCapabilities(i).ProductName;
            choices.Add(new MicrophoneChoice(name, name));
        }

        MicrophoneBox.ItemsSource = choices;
    }

    /// <summary>A label/enum pair so the provider combo shows friendly text but
    /// maps straight back to the <see cref="RefinementProvider"/> on save.</summary>
    private sealed record ProviderChoice(string Label, RefinementProvider Value);

    private void LoadFromConfig(BlurtConfig config)
    {
        TranscriptionSourceBox.SelectedItem = config.Transcription;
        // Match the stored model by size against the offered list so a hand-edited
        // quantization still selects the right entry. A stored size that matches no
        // option — e.g. a legacy "base" config (issue 18 removed that model) — falls
        // back to the small default rather than crashing or leaving a blank combo.
        ModelSizeBox.SelectedItem =
            ModelChoices.FirstOrDefault(m => m.Size == config.WhisperModel.Size)
            ?? WhisperModel.Default;

        RefinementProviderBox.SelectedValue = config.RefinementProvider;
        BaseUrlBox.Text = config.RefinementBaseUrl;
        RefinementModelBox.Text = config.RefinementModel;
        UpdateProviderHint();

        // Never surface the stored key. Show a placeholder when one exists; blank
        // means "no key yet / clear nothing". The hint adapts to which case it is.
        if (_hadApiKey)
        {
            ApiKeyBox.Password = ApiKeyPlaceholder;
            ApiKeyHint.Text = "A key is stored (encrypted). Leave as-is to keep it, or type a new one to replace it.";
        }

        HotkeyFixBox.Text = ChordFor(config, TriggerKind.Fix);
        HotkeyEnglishBox.Text = ChordFor(config, TriggerKind.English);
        HotkeyFlexBox.Text = ChordFor(config, TriggerKind.FlexSlot);

        FlexOrderBox.Text = string.Join(", ", config.FlexSlotOrder);
        CustomPromptBox.Text = config.CustomPrompt;

        OverlayAnchorBox.SelectedItem = config.OverlayAnchor;
        SoundEnabledBox.IsChecked = config.SoundEnabled;

        SelectConfiguredMicrophone(config);
    }

    // Select the microphone matching the config. FollowDefault → the "(Windows
    // default)" item; Specific → the entry whose name matches the saved device. If
    // the saved device isn't currently plugged in, add a "(not connected)" entry for
    // it so saving keeps the choice rather than silently dropping to default.
    private void SelectConfiguredMicrophone(BlurtConfig config)
    {
        var choices = (List<MicrophoneChoice>)MicrophoneBox.ItemsSource;

        if (config.InputDeviceMode == InputDeviceMode.FollowDefault)
        {
            MicrophoneBox.SelectedIndex = 0;   // the "(Windows default)" entry
            return;
        }

        var match = choices.FirstOrDefault(c => c.DeviceName == config.InputDeviceName);
        if (match is null && !string.IsNullOrEmpty(config.InputDeviceName))
        {
            match = new MicrophoneChoice($"{config.InputDeviceName} (not connected)", config.InputDeviceName);
            choices.Add(match);
            MicrophoneBox.ItemsSource = null;
            MicrophoneBox.ItemsSource = choices;
        }

        MicrophoneBox.SelectedItem = match ?? choices[0];
    }

    // Refresh the per-selection manual-install guidance (issue 18) whenever the
    // model selection changes (and once on load). Because the in-app download is
    // blocked by the corporate proxy, colleagues install the file by hand — so we
    // show the exact filename, the working resolve link, and the target folder, all
    // derived from the selected model. Never hardcoded to one model.
    private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSizeBox.SelectedItem is not WhisperModel model)
            return;

        ModelDownloadHint.Text =
            $"To install by hand, download {model.FileName} from {model.DownloadUrl} " +
            $"and place it in {ModelsDirectory}.";
    }

    // Where the runtime expects model files — derived the same way the provisioner
    // does (issue 18), so the folder shown here is exactly where Blurt loads from.
    private static string ModelsDirectory =>
        new ModelProvisioner(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            NoopDownloader.Instance).ModelsDirectory;

    // The provisioner needs a downloader to construct, but the settings window only
    // reads its ModelsDirectory — never downloads — so a no-op stand-in is enough.
    private sealed class NoopDownloader : IModelDownloader
    {
        public static readonly NoopDownloader Instance = new();
        public Task DownloadAsync(WhisperModel model, string targetPath, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static string ChordFor(BlurtConfig config, TriggerKind trigger) =>
        config.HotkeyBindings.TryGetValue(trigger, out var chord) ? chord : "";

    // Press-to-capture for the hotkey fields (issue 20): focus a field and press the
    // chord (e.g. AltGr+,) to set it, instead of typing the text by hand. The pure
    // decision — is this (AltGr, key) a valid trigger chord, and its chord string —
    // lives in Core's HotkeyCapture; this handler is the thin WPF shell that reads the
    // key state and writes the field. Manual text entry still works: when no AltGr is
    // held we don't intercept, so typing "AltGr+," by hand is unaffected.
    private void OnHotkeyPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox box)
            return;

        // When Alt is held WPF routes the keystroke as Key.System with the real key in
        // SystemKey; otherwise it's in Key. Resolve to a Win32 virtual-key code, which
        // is layout-independent (AltGr surfaces as Ctrl+Alt, so the VK is what matters).
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore bare modifier presses (AltGr/Ctrl/Shift on their own) so the field
        // doesn't react until an actual character key is pressed with the modifier.
        if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift)
            return;

        // AltGr on a German layout is RightAlt, which Windows also raises as Ctrl+Alt.
        // Accept either signal so capture works regardless of how the modifier surfaces.
        var altGrHeld = Keyboard.IsKeyDown(Key.RightAlt)
            || (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));

        // Without a modifier, let the keystroke through so the user can still type a
        // chord string by hand (the manual-entry fallback the issue requires).
        if (!altGrHeld)
            return;

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (HotkeyCapture.TryCapture(altGrHeld, vk, out var chord))
        {
            box.Text = chord;
            box.CaretIndex = chord.Length;
            HideErrors();
        }
        else
        {
            // A modifier chord that isn't a valid Blurt trigger — tell the user which
            // keys are supported rather than silently writing a bad value.
            ShowErrors(["Only AltGr + , . - can be captured as a hotkey. " +
                "Press one of those, or type the chord (e.g. AltGr+,) by hand."]);
        }

        // Either way the chord was a capture attempt, not text to insert — consume it
        // so the AltGr character doesn't also land in the field.
        e.Handled = true;
    }

    // Switching providers only re-explains the endpoint/key contract — it never
    // touches the stored key (the "(unchanged)" placeholder still preserves it on
    // save) nor rewrites the base URL, which the user owns. The local path is
    // keyless: leave the key blank and point the base URL at the Ollama endpoint.
    private void OnRefinementProviderChanged(object sender, SelectionChangedEventArgs e)
        => UpdateProviderHint();

    private void UpdateProviderHint()
    {
        if (RefinementProviderBox.SelectedValue is not RefinementProvider provider)
            return;

        ProviderHint.Text = provider == RefinementProvider.LocalOpenAiCompatible
            ? "Local endpoint (e.g. Ollama): set the base URL to http://<host>:11434/v1 and leave the API key empty. The stored key is kept but not sent."
            : "OpenAI cloud: base URL https://api.openai.com/v1 and a stored API key are used.";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var config = BuildConfigFromFields();

        var validation = SettingsValidation.Validate(config);
        if (!validation.IsValid)
        {
            ShowErrors(validation.Errors);
            return;   // don't persist; the user fixes the inline errors and retries
        }

        HideErrors();
        _store.Save(config);

        // Only write the key when the user typed a real new value. The placeholder
        // (or blank, when none was stored) means "leave the stored key alone".
        var typedKey = ApiKeyBox.Password;
        if (!string.IsNullOrEmpty(typedKey) && typedKey != ApiKeyPlaceholder)
        {
            _store.SaveApiKey(typedKey);
        }

        SavedConfig = config;
        DialogResult = true;   // closes the dialog; caller applies the runtime changes
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    // Read every field back into an immutable BlurtConfig. The hotkey and flex-order
    // text boxes are free text; they're validated (hotkeys) / best-effort parsed
    // (flex order) below. Unparseable hotkey text is left in the config as-is so
    // validation can flag it; the resolver's ResolveVkMap falls back at runtime.
    private BlurtConfig BuildConfigFromFields() =>
        _original with
        {
            Transcription = (TranscriptionMode)TranscriptionSourceBox.SelectedItem,
            WhisperModel = (WhisperModel)ModelSizeBox.SelectedItem,
            RefinementProvider = (RefinementProvider)RefinementProviderBox.SelectedValue,
            RefinementBaseUrl = BaseUrlBox.Text.Trim(),
            RefinementModel = RefinementModelBox.Text.Trim(),
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = HotkeyFixBox.Text.Trim(),
                [TriggerKind.English] = HotkeyEnglishBox.Text.Trim(),
                [TriggerKind.FlexSlot] = HotkeyFlexBox.Text.Trim(),
            },
            FlexSlotOrder = ParseFlexOrder(FlexOrderBox.Text, _original.FlexSlotOrder),
            CustomPrompt = CustomPromptBox.Text,
            OverlayAnchor = (OverlayAnchor)OverlayAnchorBox.SelectedItem,
            SoundEnabled = SoundEnabledBox.IsChecked == true,
            InputDeviceMode = SelectedMicrophoneMode(),
            InputDeviceName = SelectedMicrophoneName(),
        };

    // The chosen microphone's mode: a null DeviceName (the "(Windows default)" item)
    // means FollowDefault; a named device means Specific. Issue 16.
    private InputDeviceMode SelectedMicrophoneMode() =>
        MicrophoneBox.SelectedItem is MicrophoneChoice { DeviceName: not null }
            ? InputDeviceMode.Specific
            : InputDeviceMode.FollowDefault;

    // The product name to persist for a Specific device, or "" for FollowDefault.
    private string SelectedMicrophoneName() =>
        MicrophoneBox.SelectedItem is MicrophoneChoice { DeviceName: { } name } ? name : "";

    // Parse "Pur, Bullets, Custom" into the mode order. Unknown tokens are dropped;
    // if nothing valid remains, keep the previous order rather than emptying the
    // cycle (an empty order would leave the flex slot with no modes).
    private static IReadOnlyList<FlexSlotMode> ParseFlexOrder(
        string text, IReadOnlyList<FlexSlotMode> fallback)
    {
        var modes = new List<FlexSlotMode>();
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<FlexSlotMode>(token, ignoreCase: true, out var mode) && !modes.Contains(mode))
                modes.Add(mode);
        }

        return modes.Count > 0 ? modes : fallback;
    }

    private void ShowErrors(IReadOnlyList<string> errors)
    {
        ErrorList.ItemsSource = errors.ToList();
        ErrorPanel.Visibility = Visibility.Visible;

        // Issue 20: the error panel sits at the bottom of a scrolling form, so a
        // rejected save was easy to miss. Bring it into view (and focus it) once the
        // layout has updated, so the user actually sees why nothing was saved.
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                ErrorPanel.BringIntoView();
                ErrorPanel.Focus();
            }),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void HideErrors()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        ErrorList.ItemsSource = null;
    }
}
