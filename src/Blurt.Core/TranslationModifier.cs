namespace Blurt.Core;

/// <summary>
/// The pure "also translate to English" decision behind issue 39: a per-dictation
/// modifier (Shift, held with the trigger chord) that layers an English translation
/// on top of whatever refined mode is active. It composes at the prompt level — the
/// mode's own instruction plus a trailing "translate the result to English" directive
/// — so the layered dictation still makes a single refinement call and only text
/// crosses the network. The verbatim path (Pur, or a Custom mode with no prompt) has
/// no base prompt to layer onto and stays verbatim/zero-network: the modifier never
/// turns a local dictation into a network call.
/// </summary>
public static class TranslationModifier
{
    /// <summary>
    /// The directive appended to a mode's prompt when the modifier is held, layering
    /// an English translation on top of the mode's own transformation.
    /// </summary>
    public const string ToEnglishLayer =
        " Then translate the result into fluent, natural English and respond only in " +
        "English, whatever the input language was — keep the formatting and meaning " +
        "produced above, just in English.";

    /// <summary>
    /// The effective refinement prompt for a dictation: <paramref name="basePrompt"/>
    /// as-is when <paramref name="alsoTranslate"/> is false, or with the English layer
    /// appended when it is true. A null/blank base prompt (the verbatim path) returns
    /// <c>null</c> regardless of the modifier, so the caller keeps dictating verbatim
    /// (Pur stays zero-network).
    /// </summary>
    public static string? Compose(string? basePrompt, bool alsoTranslate)
    {
        // Verbatim path (Pur / empty Custom): nothing to layer onto, and turning it
        // into a translation would force a network call on a local dictation. Stay null.
        if (string.IsNullOrWhiteSpace(basePrompt))
            return null;

        return alsoTranslate ? basePrompt + ToEnglishLayer : basePrompt;
    }
}
