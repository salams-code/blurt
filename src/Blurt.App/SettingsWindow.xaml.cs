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

    // Privacy-tier wiring (issue 27). The tier combo and the two underlying axes
    // (transcription source, refinement provider) drive each other, so a guard
    // breaks the SelectionChanged feedback loop: while we apply a tier (or sync
    // the tier back from the axes) the opposite direction is suppressed. _loaded
    // gates the new handlers off during construction, where SelectionChanged fires
    // before the fields hold real values.
    private bool _loaded;
    private bool _syncingTier;

    // Per-provider endpoint memory (issue 24): which provider's values currently
    // sit in the Base URL/Model fields (null until LoadFromConfig has run — the
    // provider combo fires SelectionChanged during initialization), plus each
    // provider's latest values, seeded from the persisted map.
    private RefinementProvider? _fieldsProvider;
    private Dictionary<RefinementProvider, RefinementEndpoint> _endpoints = new();

    // The prompt-reset backup slot (issue 37). Seeded from the loaded config and
    // overwritten when the user clicks "Reset prompts to defaults"; persisted as part
    // of the config on Save. Held here (not in a text box) since it has no visible field.
    private PromptSnapshot? _promptBackup;

    /// <summary>
    /// The config that was persisted, set once <see cref="OnSave"/> succeeds (the
    /// window then closes). Non-null here is the save signal the caller's Closed
    /// handler gates on; null means the window was cancelled/closed without saving.
    /// </summary>
    public BlurtConfig? SavedConfig { get; private set; }

    public SettingsWindow(SettingsStore store)
    {
        // Before InitializeComponent so the XAML's StaticResource references to
        // the shared theme styles resolve during parse (issue 19).
        ThemeManager.Apply(this);
        InitializeComponent();

        _store = store;
        _original = store.Load();
        _hadApiKey = store.LoadApiKey() is { Length: > 0 };

        PopulateChoices();
        LoadFromConfig(_original);

        // Construction done: SelectionChanged from real user edits should now
        // re-wire the tier ⇄ axes relationship (LoadFromConfig set it up directly).
        _loaded = true;
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
        // Privacy tier (issue 27): the guided primary control. "Custom" (null tier)
        // is the display state for a non-standard advanced combo, not a setting the
        // user applies — selecting it is a no-op (OnPrivacyTierChanged ignores it).
        PrivacyTierBox.DisplayMemberPath = nameof(TierChoice.Label);
        PrivacyTierBox.ItemsSource = TierChoices;

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

    /// <summary>A label/tier pair for the privacy combo (issue 27). A null
    /// <see cref="Tier"/> is the "Custom" entry — the display state for an advanced
    /// combo that no tier represents; it is never applied as a setting.</summary>
    private sealed record TierChoice(string Label, PrivacyTier? Tier);

    // The tiers offered, in privacy order, plus the Custom display entry last. The
    // labels are framed by what leaves the machine, matching the per-tier hint.
    private static readonly TierChoice[] TierChoices =
    [
        new("Fully local (offline)", PrivacyTier.FullyLocal),
        new("Voice stays home (cloud refine)", PrivacyTier.VoiceStaysHome),
        new("Full cloud", PrivacyTier.FullCloud),
        new("Custom", null),
    ];

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
        _endpoints = new Dictionary<RefinementProvider, RefinementEndpoint>(config.RefinementEndpoints);
        _fieldsProvider = config.RefinementProvider;   // fields now hold this provider's values
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

        // Editable per-mode prompts (issue 35), each pre-filled with the stored
        // value — which is the shipped default on an untouched install.
        FixPromptBox.Text = config.FixPrompt;
        EnglishPromptBox.Text = config.EnglishPrompt;
        BulletsPromptBox.Text = config.BulletsPrompt;
        EmailPromptBox.Text = config.EmailPrompt;
        _promptBackup = config.PromptBackup;   // issue 37: carry the existing backup forward
        RefreshBackupView();                   // issue 38: reflect the backup in the UI

        FlexOrderBox.Text = string.Join(", ", config.FlexSlotOrder);
        CustomPromptBox.Text = config.CustomPrompt;

        OverlayAnchorBox.SelectedItem = config.OverlayAnchor;
        SoundEnabledBox.IsChecked = config.SoundEnabled;

        // Auto-start reflects the actual Run-key state, not config — the registry
        // is the source of truth (the user may toggle it via Windows settings too).
        StartWithWindowsBox.IsChecked = WindowsStartup.IsEnabled();

        SelectConfiguredMicrophone(config);

        // Derive the privacy tier from the loaded axes (issue 27). Done directly
        // (not via the SelectionChanged path) because _loaded is still false here;
        // selecting the tier item now would otherwise no-op. A loaded combo that
        // matches no tier ("Custom") reveals the advanced controls so the user can
        // see what it actually is.
        SelectTierFromCurrentAxes();
        UpdatePrivacyHint();
        if (CurrentAxesTier() is null)
        {
            ShowAdvancedTranscriptionBox.IsChecked = true;   // fires the toggle → panel visible
        }
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

    // Copy affordances for the manual-install guidance (issue 25): the URL and the
    // target folder are the two strings a blocked colleague pastes into a browser
    // and Explorer — both derived from the current selection, like the hint itself.
    private void OnCopyModelLink(object sender, RoutedEventArgs e)
    {
        if (ModelSizeBox.SelectedItem is WhisperModel model)
            ClipboardCopy.WithFeedback((System.Windows.Controls.Button)sender, model.DownloadUrl);
    }

    private void OnCopyModelFolder(object sender, RoutedEventArgs e)
        => ClipboardCopy.WithFeedback((System.Windows.Controls.Button)sender, ModelsDirectory);

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

    // Switching providers treats the two as separate paths (issue 24): the
    // current field values are remembered for the provider being left, and the
    // target provider's remembered (or default) base URL + model are swapped in —
    // so an Ollama URL never lingers under "OpenAI" and edits survive toggling.
    // The pure decision lives in Core's ProviderEndpoints.Switch; the stored API
    // key is never touched (the "(unchanged)" placeholder still preserves it).
    private void OnRefinementProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        // Fires during initialization (before LoadFromConfig) — only act on a
        // real user switch, once the fields belong to a known provider.
        if (_fieldsProvider is { } from
            && RefinementProviderBox.SelectedValue is RefinementProvider to
            && to != from)
        {
            var (target, remembered) = ProviderEndpoints.Switch(
                from, FieldsEndpoint(), to, _endpoints);

            _endpoints = new Dictionary<RefinementProvider, RefinementEndpoint>(remembered);
            _fieldsProvider = to;
            BaseUrlBox.Text = target.BaseUrl;
            RefinementModelBox.Text = target.Model;
        }

        UpdateProviderHint();
        SyncPrivacyTierDisplay();   // a manual provider change may move us to/from a tier (issue 27)
    }

    // The endpoint currently shown in the Base URL/Model fields.
    private RefinementEndpoint FieldsEndpoint() => new()
    {
        BaseUrl = BaseUrlBox.Text.Trim(),
        Model = RefinementModelBox.Text.Trim(),
    };

    private void UpdateProviderHint()
    {
        if (RefinementProviderBox.SelectedValue is not RefinementProvider provider)
            return;

        ProviderHint.Text = provider == RefinementProvider.LocalOpenAiCompatible
            ? "Local endpoint (e.g. Ollama): set the base URL to http://<host>:11434/v1 and leave the API key empty. The stored key is kept but not sent."
            : "OpenAI cloud: base URL https://api.openai.com/v1 and a stored API key are used.";
    }

    // Privacy tier → axes (issue 27). Selecting a real tier sets both the
    // transcription source and the refinement provider via Core's mapping; the
    // resulting SelectionChanged on those combos is suppressed (_syncingTier) so it
    // doesn't bounce back and re-pick the tier. Selecting "Custom" (null tier) is a
    // no-op — it's only ever shown, set by SyncPrivacyTierDisplay, never applied.
    private void OnPrivacyTierChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || _syncingTier)
            return;

        if (PrivacyTierBox.SelectedItem is not TierChoice { Tier: { } tier })
            return;

        var (transcription, refinement) = PrivacyTiers.SettingsFor(tier);

        _syncingTier = true;
        TranscriptionSourceBox.SelectedItem = transcription;
        // Setting SelectedValue still runs OnRefinementProviderChanged's endpoint
        // swap (issue 24); only its tier re-sync is guarded out here.
        RefinementProviderBox.SelectedValue = refinement;
        _syncingTier = false;

        UpdatePrivacyHint();
    }

    // Axes → privacy tier (issue 27): a manual change to the source or provider
    // re-derives which tier (if any) the current combo is, so the combo follows or
    // falls to "Custom". Guarded so a tier-driven change doesn't re-enter.
    private void OnTranscriptionSourceChanged(object sender, SelectionChangedEventArgs e)
        => SyncPrivacyTierDisplay();

    private void SyncPrivacyTierDisplay()
    {
        if (!_loaded || _syncingTier)
            return;

        _syncingTier = true;
        SelectTierFromCurrentAxes();
        _syncingTier = false;

        UpdatePrivacyHint();
    }

    // Point the tier combo at whatever the current source + provider classify as,
    // or the "Custom" entry (null tier) when no tier matches.
    private void SelectTierFromCurrentAxes()
    {
        var tier = CurrentAxesTier();
        PrivacyTierBox.SelectedItem = System.Array.Find(TierChoices, c => c.Tier == tier);
    }

    // The tier the currently-selected source + provider correspond to, or null
    // ("Custom") for a non-standard combo.
    private PrivacyTier? CurrentAxesTier() =>
        TranscriptionSourceBox.SelectedItem is TranscriptionMode transcription
        && RefinementProviderBox.SelectedValue is RefinementProvider refinement
            ? PrivacyTiers.Classify(transcription, refinement)
            : null;

    private void UpdatePrivacyHint() =>
        PrivacyHint.Text = (PrivacyTierBox.SelectedItem as TierChoice)?.Tier switch
        {
            PrivacyTier.FullyLocal =>
                "Your voice and the transcript both stay on this PC — nothing is sent to the cloud (fully offline).",
            PrivacyTier.VoiceStaysHome =>
                "Your voice stays on this PC; only the transcribed text is sent to OpenAI for refinement. Needs the API key below.",
            PrivacyTier.FullCloud =>
                "Your voice and the text are both sent to OpenAI (fastest, best quality). Needs the API key below.",
            _ =>
                "Custom combination — set by the advanced transcription source and the refinement provider below.",
        };

    // Advanced disclosure toggle (issue 27): reveal/hide the underlying source and
    // local-model controls. Null-guarded for the parse-time order.
    private void OnToggleAdvancedTranscription(object sender, RoutedEventArgs e)
    {
        if (AdvancedTranscriptionPanel is null)
            return;

        AdvancedTranscriptionPanel.Visibility =
            ShowAdvancedTranscriptionBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
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

        // Apply auto-start directly to the Run key (its presence is the source of
        // truth; nothing about it lives in BlurtConfig).
        WindowsStartup.SetEnabled(StartWithWindowsBox.IsChecked == true);

        SavedConfig = config;
        Close();   // modeless window: just close it. The Closed handler applies the
                   // saved config (gated on SavedConfig, not DialogResult — which is
                   // illegal to set on a Show()-modeless window and would crash).
    }

    // Reset every editable prompt to its shipped default (issue 37). The decision —
    // back up the current prompts into the single slot, then apply the defaults, and
    // no-op when nothing is customised — lives in Core's PromptReset; this just feeds
    // it the prompts currently in the fields and reflects the result back. The backup
    // and the defaults persist together on Save; the restore UI is issue 38.
    private void OnResetPrompts(object sender, RoutedEventArgs e)
    {
        var reset = PromptReset.Reset(BuildConfigFromFields());

        _promptBackup = reset.PromptBackup;
        FixPromptBox.Text = reset.FixPrompt;
        EnglishPromptBox.Text = reset.EnglishPrompt;
        BulletsPromptBox.Text = reset.BulletsPrompt;
        EmailPromptBox.Text = reset.EmailPrompt;
        CustomPromptBox.Text = reset.CustomPrompt;
        RefreshBackupView();   // issue 38: a reset just (re)created the backup
    }

    // Prompt-backup UI (issue 38): see / copy / restore the snapshot a reset created.
    // The view text and clipboard wording come from Core's PromptBackupText so both
    // match; Copy/Restore are disabled when there is no backup.
    private void RefreshBackupView()
    {
        var hasBackup = _promptBackup is not null;

        BackupStatusText.Text = hasBackup
            ? "A backup of your previous prompts is available (from your last reset)."
            : "No backup yet - use 'Reset prompts to defaults' to create one.";
        BackupViewBox.Text = hasBackup ? PromptBackupText.Format(_promptBackup!) : "";
        // Keep the text collapsed by default (and re-collapse after a reset) so it
        // doesn't sit open in the way; the user expands it on demand.
        BackupViewBox.Visibility = Visibility.Collapsed;
        ToggleBackupButton.Content = "Show backed-up prompts";
        RestoreBackupButton.IsEnabled = hasBackup;
        CopyBackupButton.IsEnabled = hasBackup;
        ToggleBackupButton.IsEnabled = hasBackup;
    }

    private void OnToggleBackupView(object sender, RoutedEventArgs e)
    {
        var show = BackupViewBox.Visibility != Visibility.Visible;
        BackupViewBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ToggleBackupButton.Content = show ? "Hide backed-up prompts" : "Show backed-up prompts";
    }

    private void OnCopyBackup(object sender, RoutedEventArgs e)
    {
        if (_promptBackup is not null)
            ClipboardCopy.WithFeedback(
                (System.Windows.Controls.Button)sender, PromptBackupText.Format(_promptBackup));
    }

    // Restore the backed-up prompts into the live prompt fields (Core's ApplyTo). The
    // backup slot is kept so the user can restore again; Save persists the restored
    // prompts, so they take effect on the next dictation with no restart.
    private void OnRestoreBackup(object sender, RoutedEventArgs e)
    {
        if (_promptBackup is null)
            return;

        var restored = _promptBackup.ApplyTo(BuildConfigFromFields());
        FixPromptBox.Text = restored.FixPrompt;
        EnglishPromptBox.Text = restored.EnglishPrompt;
        BulletsPromptBox.Text = restored.BulletsPrompt;
        EmailPromptBox.Text = restored.EmailPrompt;
        CustomPromptBox.Text = restored.CustomPrompt;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close();   // modeless: close without touching DialogResult. SavedConfig stays
                   // null, so the Closed handler treats this as a cancel.
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
            // Issue 24: persist both providers' endpoints — the map remembered
            // from switches, with the visible fields as the active provider's
            // latest word.
            RefinementEndpoints = new Dictionary<RefinementProvider, RefinementEndpoint>(_endpoints)
            {
                [(RefinementProvider)RefinementProviderBox.SelectedValue] = FieldsEndpoint(),
            },
            HotkeyBindings = new Dictionary<TriggerKind, string>
            {
                [TriggerKind.Fix] = HotkeyFixBox.Text.Trim(),
                [TriggerKind.English] = HotkeyEnglishBox.Text.Trim(),
                [TriggerKind.FlexSlot] = HotkeyFlexBox.Text.Trim(),
            },
            FlexSlotOrder = ParseFlexOrder(FlexOrderBox.Text, _original.FlexSlotOrder),
            // Editable per-mode prompts (issue 35): persisted verbatim. A blank
            // always-on prompt self-heals to its default at resolution time
            // (ModePrompts.For); Custom keeps its blank-means-no-refiner contract.
            FixPrompt = FixPromptBox.Text,
            EnglishPrompt = EnglishPromptBox.Text,
            BulletsPrompt = BulletsPromptBox.Text,
            EmailPrompt = EmailPromptBox.Text,
            CustomPrompt = CustomPromptBox.Text,
            PromptBackup = _promptBackup,   // issue 37: persist the reset backup slot
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
