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

    /// <summary>Rewrite conversational speech into a well-formed email (LLM, issue 36).</summary>
    Email,
}

/// <summary>
/// A refined dictation mode whose system prompt the user can edit (issue 35):
/// Fix, English, Bullets and Custom. Pur is deliberately absent — it is verbatim,
/// promptless and zero-network, and must stay that way. <see cref="ModePrompts"/>
/// pairs each mode with its editable prompt (default or override).
/// </summary>
public enum RefinedMode
{
    /// <summary>Clean up the German transcript without changing its meaning.</summary>
    Fix,

    /// <summary>Translate the transcript into fluent English.</summary>
    English,

    /// <summary>Reformat the transcript into bullet points.</summary>
    Bullets,

    /// <summary>Apply the user-defined prompt (no built-in default wording).</summary>
    Custom,

    /// <summary>Rewrite conversational speech into a well-formed email.</summary>
    Email,
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
    /// GPU-acceleration preference for local transcription (ADR-0001, issue 42).
    /// <see cref="GpuPreference.Auto"/> (the default) prefers the Vulkan backend and
    /// falls back to CPU automatically; <see cref="GpuPreference.Off"/> forces CPU.
    /// Auto is the zero value, so a config written before this setting existed has no
    /// key for it and deserialises to GPU-on after an upgrade.
    /// </summary>
    public GpuPreference GpuPreference { get; init; } = GpuPreference.Auto;

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
    /// A non-OpenAI host the user has explicitly confirmed may receive the API key
    /// (security finding F1) — e.g. an authenticated gateway like OpenRouter or a
    /// self-hosted proxy. With the <see cref="RefinementProvider.OpenAi"/> provider
    /// the key is sent only to OpenAI's own host, a loopback address, or this host;
    /// any other base URL (a tricked "faster proxy" or a tampered config.json) gets
    /// no key, so the credential can't be silently exfiltrated. Empty by default —
    /// until the user confirms a custom host, the key never leaves for a stranger.
    /// Stored as the bare host name (no scheme/port).
    /// </summary>
    public string TrustedKeyHost { get; init; } = "";

    /// <summary>
    /// Per-provider endpoint memory (issue 24): each provider's last base URL +
    /// model, so switching providers in Settings swaps fields instead of leaving
    /// the other provider's values behind, and switching back restores edits.
    /// The active endpoint stays <see cref="RefinementBaseUrl"/>/<see cref="RefinementModel"/>
    /// (runtime reads those; configs from before this setting keep working) —
    /// this map remembers the rest. Empty by default: a provider that was never
    /// visited resolves to <see cref="ProviderEndpoints.DefaultFor"/>.
    /// </summary>
    public IReadOnlyDictionary<RefinementProvider, RefinementEndpoint> RefinementEndpoints { get; init; } =
        new Dictionary<RefinementProvider, RefinementEndpoint>();

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

    /// <summary>The order the Flex slot cycles its modes in (default: Pur → Bullets → Custom → Email; Email added in issue 36).</summary>
    public IReadOnlyList<FlexSlotMode> FlexSlotOrder { get; init; } =
        [FlexSlotMode.Pur, FlexSlotMode.Bullets, FlexSlotMode.Custom, FlexSlotMode.Email];

    /// <summary>
    /// The Fix mode's editable system prompt (issue 35). Defaults to the shipped
    /// <see cref="RefinementPrompts.Fix"/> so an untouched install behaves exactly
    /// as before; a config written before this setting existed has no key for it and
    /// deserialises to this default. Resolved per dictation via <see cref="ModePrompts"/>
    /// so an edit takes effect without a restart; a blanked field falls back to the
    /// default (an always-on mode can't be silently disabled).
    /// </summary>
    public string FixPrompt { get; init; } = RefinementPrompts.Fix;

    /// <summary>The English mode's editable system prompt (issue 35). See <see cref="FixPrompt"/>.</summary>
    public string EnglishPrompt { get; init; } = RefinementPrompts.English;

    /// <summary>The Bullets mode's editable system prompt (issue 35). See <see cref="FixPrompt"/>.</summary>
    public string BulletsPrompt { get; init; } = RefinementPrompts.Bullets;

    /// <summary>
    /// The Email mode's editable system prompt (issue 36). Like the other always-on
    /// modes it defaults to its shipped <see cref="RefinementPrompts.Email"/> wording
    /// and a blanked field falls back to that default (Email always refines, so it
    /// can't be silently disabled); resolved per dictation via <see cref="ModePrompts"/>.
    /// A config written before this setting existed has no key for it and deserialises
    /// to this default.
    /// </summary>
    public string EmailPrompt { get; init; } = RefinementPrompts.Email;

    /// <summary>
    /// User-defined prompt applied by the <see cref="FlexSlotMode.Custom"/> slot.
    /// Unlike the always-on prompts above it ships empty: a blank Custom prompt
    /// means "no refiner" (verbatim), so it is never replaced by a default.
    /// </summary>
    public string CustomPrompt { get; init; } = "";

    /// <summary>
    /// The single backup slot for a prompt reset (issue 37): the editable prompts as
    /// they were just before the user last reset them to defaults, so a reset is
    /// always recoverable. <c>null</c> until the first reset (and on a config written
    /// before this slot existed); overwritten on each reset (only the most recent
    /// pre-reset state is kept). <see cref="PromptReset"/> writes it; the restore UI
    /// is issue 38.
    /// </summary>
    public PromptSnapshot? PromptBackup { get; init; } = null;

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

    /// <summary>
    /// Whether the one-time driver-missing nudge (issue 45) has already been shown and
    /// dismissed. Persisted so the conservative "install/repair your GPU driver" notice
    /// fires at most once across launches. Defaults to <c>false</c> (never shown yet);
    /// a config written before this setting existed has no key for it and resolves to
    /// false, so an eligible machine still gets the one nudge after upgrade.
    /// </summary>
    public bool GpuDriverNudgeDismissed { get; init; } = false;

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
            && GpuPreference == other.GpuPreference
            && RefinementProvider == other.RefinementProvider
            && RefinementBaseUrl == other.RefinementBaseUrl
            && RefinementModel == other.RefinementModel
            && TrustedKeyHost == other.TrustedKeyHost
            && FixPrompt == other.FixPrompt
            && EnglishPrompt == other.EnglishPrompt
            && BulletsPrompt == other.BulletsPrompt
            && EmailPrompt == other.EmailPrompt
            && CustomPrompt == other.CustomPrompt
            && PromptBackup == other.PromptBackup
            && OverlayAnchor == other.OverlayAnchor
            && SoundEnabled == other.SoundEnabled
            && OnboardingCompleted == other.OnboardingCompleted
            && GpuDriverNudgeDismissed == other.GpuDriverNudgeDismissed
            && InputDeviceMode == other.InputDeviceMode
            && InputDeviceName == other.InputDeviceName
            && HotkeyBindingsEqual(HotkeyBindings, other.HotkeyBindings)
            && FlexSlotOrder.SequenceEqual(other.FlexSlotOrder)
            && EndpointsEqual(RefinementEndpoints, other.RefinementEndpoints);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Transcription);
        hash.Add(WhisperModel);
        hash.Add(GpuPreference);
        hash.Add(RefinementProvider);
        hash.Add(RefinementBaseUrl);
        hash.Add(RefinementModel);
        hash.Add(TrustedKeyHost);
        hash.Add(FixPrompt);
        hash.Add(EnglishPrompt);
        hash.Add(BulletsPrompt);
        hash.Add(EmailPrompt);
        hash.Add(CustomPrompt);
        hash.Add(PromptBackup);
        hash.Add(OverlayAnchor);
        hash.Add(SoundEnabled);
        hash.Add(OnboardingCompleted);
        hash.Add(GpuDriverNudgeDismissed);
        hash.Add(InputDeviceMode);
        hash.Add(InputDeviceName);
        foreach (var binding in HotkeyBindings.OrderBy(b => b.Key))
        {
            hash.Add(binding.Key);
            hash.Add(binding.Value);
        }
        foreach (var mode in FlexSlotOrder)
            hash.Add(mode);
        foreach (var endpoint in RefinementEndpoints.OrderBy(e => e.Key))
        {
            hash.Add(endpoint.Key);
            hash.Add(endpoint.Value);
        }
        return hash.ToHashCode();
    }

    private static bool EndpointsEqual(
        IReadOnlyDictionary<RefinementProvider, RefinementEndpoint> a,
        IReadOnlyDictionary<RefinementProvider, RefinementEndpoint> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || other != value)
                return false;
        }
        return true;
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
