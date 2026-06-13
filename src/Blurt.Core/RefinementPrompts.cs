namespace Blurt.Core;

/// <summary>
/// The system prompts that drive each refined dictation mode. Kept as a small,
/// pure source of strings (no I/O, no dependencies) so the exact wording is one
/// reviewable place and is unit-testable. <see cref="FlexSlotPrompts"/> pairs
/// these with the Flex-slot modes; the Custom mode has no constant here because
/// its prompt comes from the user's <see cref="BlurtConfig.CustomPrompt"/>.
/// </summary>
public static class RefinementPrompts
{
    /// <summary>
    /// The Fix-mode system prompt: clean up a German voice transcript without
    /// changing its meaning. Whisper output of spontaneous speech carries filler
    /// words ("ähm", "also"), missing punctuation and small grammar slips; the
    /// refiner repairs those and returns polished German only — never a
    /// translation, an answer, or commentary.
    /// </summary>
    public const string Fix =
        "You are a German writing assistant. Clean up the user's dictated German " +
        "text: fix grammar, spelling and punctuation, and remove filler words " +
        "(e.g. \"ähm\", \"also\", \"halt\") and false starts. Preserve the original " +
        "meaning, tone and wording as much as possible — do not translate, answer, " +
        "summarise or add anything. Always respond in German with only the cleaned-up " +
        "text and no extra explanation.";

    /// <summary>
    /// The English-mode system prompt: translate the user's (German) voice
    /// transcript into fluent, natural English. Whisper output of spontaneous
    /// speech carries filler words and missing punctuation; the translation
    /// smooths those out as part of producing idiomatic English. The model
    /// returns only the translation — never an answer, a summary or commentary.
    /// </summary>
    public const string English =
        "You are a translation assistant. Translate the user's dictated transcript " +
        "into fluent, natural English. The source is spontaneous German speech, so " +
        "drop filler words (e.g. \"ähm\", \"also\", \"halt\") and false starts and " +
        "render it as clean, idiomatic English while preserving the original " +
        "meaning and tone. Do not answer, summarise or add anything. Respond with " +
        "only the English translation and no extra explanation.";

    /// <summary>
    /// The Bullets-mode system prompt: turn a free-flowing dictation into a tidy
    /// bullet list. Unlike Fix it is language-agnostic — it keeps whatever
    /// language the speaker used — and it reshapes the text into bullets rather
    /// than just cleaning it up. The model returns only the bullet list, with no
    /// heading, preamble or trailing commentary, so the result drops straight in
    /// at the cursor.
    /// </summary>
    public const string Bullets =
        "You are a formatting assistant. Reformat the user's dictated transcript " +
        "into concise, well-structured bullet points. Keep the original language " +
        "of the input — do not translate. Preserve all the meaning, remove only " +
        "filler words and false starts, and do not answer, summarise away detail, " +
        "or add anything not in the transcript. Respond with only the bullet list " +
        "(one \"- \" item per line) and no heading or extra explanation.";

    /// <summary>
    /// The Email-mode system prompt (issue 36): turn the conversational way the
    /// user talks into a proper, well-formed email of the same content. The speaker
    /// dictates as if talking to a person; the model reshapes it into email form —
    /// a fitting greeting, a readable body, a sign-off where one belongs — rather
    /// than a verbatim transcript. Like Bullets it is language-agnostic (keeps the
    /// speaker's language) and content-preserving: it never answers the message,
    /// invents facts or adds commentary, and returns only the email text.
    /// </summary>
    public const string Email =
        "You are an email-writing assistant. The user has dictated, in casual " +
        "conversational speech, what they want to communicate. Rewrite it as a " +
        "clear, well-formed email that conveys the same content and intent: open " +
        "with a fitting greeting, organise the body into readable sentences and " +
        "paragraphs, and close with a sign-off when one fits. Keep the language of " +
        "the dictation — do not translate. Preserve the meaning and every specific " +
        "(names, dates, numbers, requests); remove only filler words and false " +
        "starts. Do not answer the message, invent facts, or add commentary. " +
        "Respond with only the email text and no extra explanation.";
}
