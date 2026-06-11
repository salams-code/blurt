using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Blurt.Core;

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
    }

    // The local models the window offers, in display order. The download guidance
    // and the load-back match (LoadFromConfig) both derive from this one list, so
    // adding a model is a single edit and a stored, now-unknown size (e.g. the
    // removed "base") simply falls back to the first entry.
    private static readonly WhisperModel[] ModelChoices = [WhisperModel.Default, WhisperModel.Turbo];

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

        BaseUrlBox.Text = config.RefinementBaseUrl;
        RefinementModelBox.Text = config.RefinementModel;

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
        };

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
    }

    private void HideErrors()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        ErrorList.ItemsSource = null;
    }
}
