namespace Blurt.Core;

/// <summary>
/// The four steps the first-run wizard walks, in cycle order (issue 15): pick a
/// microphone and confirm a live level, paste an OpenAI API key (optional),
/// fetch the Whisper model, then review the hotkey bindings. Pure ordering — no
/// UI — so the wizard's sequence is unit-testable and a reorder is a deliberate
/// change.
/// </summary>
public enum OnboardingStep
{
    /// <summary>Choose an input device and confirm a visible signal level.</summary>
    Microphone,

    /// <summary>Step-by-step OpenAI API-key guide + entry (DPAPI-stored, skippable).</summary>
    ApiKey,

    /// <summary>Ensure the Whisper model is present (download only if missing).</summary>
    Model,

    /// <summary>Show the current hotkey bindings with a pointer to remap them later.</summary>
    Hotkeys,
}

/// <summary>
/// Decides whether the guided first-run onboarding (issue 15) should run. A pure
/// function over <see cref="BlurtConfig"/> so the "runs only once" rule is
/// unit-testable offline.
/// </summary>
public static class Onboarding
{
    /// <summary>
    /// Whether onboarding still needs to run. The completion flag is the single
    /// source of truth: a fresh install has no config.json, so
    /// <see cref="SettingsStore.Load"/> returns <see cref="BlurtConfig.Default"/>
    /// with <see cref="BlurtConfig.OnboardingCompleted"/> <c>false</c> → needed;
    /// the wizard persists <c>true</c> on finish → never needed again, even when
    /// the user skipped the optional API key.
    /// </summary>
    public static bool IsNeeded(BlurtConfig config) => !config.OnboardingCompleted;
}
