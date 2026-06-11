namespace Blurt.Core;

/// <summary>Where speech → text happens: on-device Whisper, or the OpenAI Whisper API.</summary>
public enum TranscriptionMode
{
    Local,
    Online,
}

/// <summary>
/// Which refinement endpoint is active (issue 17). Both speak the same
/// OpenAI-compatible Chat Completions protocol — the difference is whether the
/// stored API key is sent. <see cref="OpenAi"/> sends it; a
/// <see cref="LocalOpenAiCompatible"/> endpoint (e.g. Ollama) needs none, so the
/// key is kept stored but not sent. Declared first → default 0 → matches today's
/// behaviour for any config written before this setting existed.
/// </summary>
public enum RefinementProvider
{
    /// <summary>OpenAI cloud: send the stored API key as Bearer auth.</summary>
    OpenAi,

    /// <summary>A local/remote OpenAI-compatible endpoint (Ollama): send no key.</summary>
    LocalOpenAiCompatible,
}

/// <summary>
/// One Flex-slot refinement mode. The slot cycles through these in declaration
/// order (design contract §2: Pur → Bullets → Custom → Pur …).
/// </summary>
public enum FlexSlotMode
{
    /// <summary>Verbatim Whisper output, no LLM call (the only fully offline mode).</summary>
    Pur,

    /// <summary>Reformat the dictation into clean bullet points (LLM).</summary>
    Bullets,

    /// <summary>Apply the user-defined <see cref="BlurtConfig.CustomPrompt"/> (LLM).</summary>
    Custom,
}

/// <summary>Where the status overlay positions itself (design contract §9).</summary>
public enum OverlayAnchor
{
    /// <summary>Follow the mouse pointer.</summary>
    MousePointer,

    /// <summary>Fixed at the bottom-centre of the screen.</summary>
    BottomCenter,
}

/// <summary>
/// All non-secret, user-visible configuration, persisted as readable JSON at
/// <c>&lt;appDataRoot&gt;\Blurt\config.json</c>. The API key is deliberately
/// <em>not</em> here — it lives encrypted in a separate file (see
/// <see cref="SettingsStore"/>). A <c>record</c> so round-trip equality is
/// value-based and free.
/// </summary>
public sealed record BlurtConfig
{
    /// <summary>Local on-device Whisper vs. the OpenAI Whisper API.</summary>
    public TranscriptionMode Transcription { get; init; } = TranscriptionMode.Local;

    /// <summary>Whisper model to use in local mode (design default: <c>small</c>, q5).</summary>
    public WhisperModel WhisperModel { get; init; } = WhisperModel.Default;

    /// <summary>
    /// Which refinement endpoint is active (issue 17): the OpenAI cloud (send the
    /// stored key) or a local/Ollama OpenAI-compatible endpoint (send no key, but
    /// keep it stored). Gates whether the key is sent; <see cref="RefinementBaseUrl"/>
    /// stays the user-editable source of truth for the endpoint address.
    /// Default <see cref="RefinementProvider.OpenAi"/> matches today's behaviour.
    /// </summary>
    public RefinementProvider RefinementProvider { get; init; } = RefinementProvider.OpenAi;

    /// <summary>Base URL of the OpenAI-compatible refinement endpoint.</summary>
    public string RefinementBaseUrl { get; init; } = "https://api.openai.com/v1";

    /// <summary>Refinement model name (design default: <c>gpt-4o-mini</c>).</summary>
    public string RefinementModel { get; init; } = "gpt-4o-mini";

    /// <summary>
    /// Hotkey bindings as a map from trigger to its key chord description.
    /// Kept as strings so the JSON stays human-readable and the binding format
    /// can evolve without a schema migration (design default: AltGr + , . -).
    /// </summary>
    public IReadOnlyDictionary<TriggerKind, string> HotkeyBindings { get; init; } =
        new Dictionary<TriggerKind, string>
        {
            [TriggerKind.Fix] = "AltGr+,",
            [TriggerKind.English] = "AltGr+.",
            [TriggerKind.FlexSlot] = "AltGr+-",
        };

    /// <summary>The order the Flex slot cycles its modes in (design default: Pur → Bullets → Custom).</summary>
    public IReadOnlyList<FlexSlotMode> FlexSlotOrder { get; init; } =
        [FlexSlotMode.Pur, FlexSlotMode.Bullets, FlexSlotMode.Custom];

    /// <summary>User-defined prompt applied by the <see cref="FlexSlotMode.Custom"/> slot.</summary>
    public string CustomPrompt { get; init; } = "";

    /// <summary>Where the status overlay anchors itself.</summary>
    public OverlayAnchor OverlayAnchor { get; init; } = OverlayAnchor.MousePointer;

    /// <summary>Start/stop sound (off by default — meeting-friendly, design §9).</summary>
    public bool SoundEnabled { get; init; } = false;

    /// <summary>
    /// Whether the guided first-run onboarding (issue 15) has been completed. The
    /// single source of truth for "run the wizard?": a fresh install has no
    /// config.json, so <see cref="SettingsStore.Load"/> returns
    /// <see cref="Default"/> with this <c>false</c> → onboarding is needed; the
    /// wizard persists <c>true</c> on finish → it never runs again, even if the
    /// user skipped the optional API key. Defaults to <c>false</c>.
    /// </summary>
    public bool OnboardingCompleted { get; init; } = false;

    /// <summary>
    /// How dictation picks its capture device (issue 16). Defaults to
    /// <see cref="InputDeviceMode.FollowDefault"/> — the pre-issue-16 behaviour of
    /// always recording from the Windows default input — so unplugging/replacing a
    /// device (e.g. a Bluetooth headset) just works without reconfiguring.
    /// </summary>
    public InputDeviceMode InputDeviceMode { get; init; } = InputDeviceMode.FollowDefault;

    /// <summary>
    /// The chosen capture device's <c>WaveInCapabilities.ProductName</c>, used when
    /// <see cref="InputDeviceMode"/> is <see cref="InputDeviceMode.Specific"/>. The
    /// product name is the only handle NAudio exposes and isn't guaranteed unique;
    /// resolution matches by name (see <see cref="InputDeviceResolver"/>) and falls
    /// soft back to the default if it's gone. Empty in follow-default mode.
    /// </summary>
    public string InputDeviceName { get; init; } = "";

    /// <summary>The fully-defaulted configuration used when no config file exists yet.</summary>
    public static BlurtConfig Default { get; } = new();

    // A record's synthesised equality compares the collection properties by
    // reference, so a JSON round-trip (which rebuilds them) would never be
    // "equal". Override equality to compare those two members structurally;
    // the rest is delegated to the compiler-generated member comparisons.
    public bool Equals(BlurtConfig? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Transcription == other.Transcription
            && WhisperModel == other.WhisperModel
            && RefinementProvider == other.RefinementProvider
            && RefinementBaseUrl == other.RefinementBaseUrl
            && RefinementModel == other.RefinementModel
            && CustomPrompt == other.CustomPrompt
            && OverlayAnchor == other.OverlayAnchor
            && SoundEnabled == other.SoundEnabled
            && OnboardingCompleted == other.OnboardingCompleted
            && InputDeviceMode == other.InputDeviceMode
            && InputDeviceName == other.InputDeviceName
            && HotkeyBindingsEqual(HotkeyBindings, other.HotkeyBindings)
            && FlexSlotOrder.SequenceEqual(other.FlexSlotOrder);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Transcription);
        hash.Add(WhisperModel);
        hash.Add(RefinementProvider);
        hash.Add(RefinementBaseUrl);
        hash.Add(RefinementModel);
        hash.Add(CustomPrompt);
        hash.Add(OverlayAnchor);
        hash.Add(SoundEnabled);
        hash.Add(OnboardingCompleted);
        hash.Add(InputDeviceMode);
        hash.Add(InputDeviceName);
        foreach (var binding in HotkeyBindings.OrderBy(b => b.Key))
        {
            hash.Add(binding.Key);
            hash.Add(binding.Value);
        }
        foreach (var mode in FlexSlotOrder)
            hash.Add(mode);
        return hash.ToHashCode();
    }

    private static bool HotkeyBindingsEqual(
        IReadOnlyDictionary<TriggerKind, string> a,
        IReadOnlyDictionary<TriggerKind, string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || other != value)
                return false;
        }
        return true;
    }
}

/// <summary>
/// Narrow seam over symmetric secret protection, so <see cref="SettingsStore"/>
/// can be unit-tested with a reversible fake instead of real DPAPI. The real
/// implementation is <see cref="DpapiSecretProtector"/>.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts <paramref name="plaintext"/> into opaque bytes.</summary>
    byte[] Protect(byte[] plaintext);

    /// <summary>Reverses <see cref="Protect"/>, recovering the original bytes.</summary>
    byte[] Unprotect(byte[] ciphertext);
}
