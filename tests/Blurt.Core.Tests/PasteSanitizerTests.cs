using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class PasteSanitizerTests
{
    private const char Esc = (char)0x1b;   // terminal escape
    private const char Nul = (char)0x00;
    private const char Backspace = (char)0x08;

    [Fact]
    public void Plain_text_is_left_unchanged()
    {
        Assert.Equal("hallo welt", PasteSanitizer.Sanitize("hallo welt"));
    }

    [Fact]
    public void Umlauts_and_other_printable_unicode_survive()
    {
        Assert.Equal("schöne Grüße 🎤", PasteSanitizer.Sanitize("schöne Grüße 🎤"));
    }

    [Fact]
    public void Dangerous_control_characters_are_stripped()
    {
        // ESC (terminal escape sequences), NUL and backspace never come from real
        // speech but turn a paste into terminal control input — they must go,
        // leaving the surrounding printable text intact.
        var input = $"a{Esc}[31mb{Nul}c{Backspace}d";

        Assert.Equal("a[31mbcd", PasteSanitizer.Sanitize(input));
    }

    [Fact]
    public void Carriage_returns_are_normalised_to_line_feeds()
    {
        Assert.Equal("line1\nline2\nline3", PasteSanitizer.Sanitize("line1\r\nline2\rline3"));
    }

    [Fact]
    public void Internal_newlines_and_tabs_are_kept_so_multiline_dictation_survives()
    {
        // A legitimate multi-line / indented refinement (a list, a code block) must
        // not be flattened — only the dangerous controls are removed.
        Assert.Equal("line1\nline2\tend", PasteSanitizer.Sanitize("line1\nline2\tend"));
    }

    [Fact]
    public void A_trailing_newline_is_trimmed_so_the_last_line_cannot_auto_submit()
    {
        // The core of finding F2: a provider-supplied trailing newline would let
        // the final (most dangerous) line auto-submit in a focused shell.
        Assert.Equal("ls\nrm -rf ~", PasteSanitizer.Sanitize("ls\nrm -rf ~\n"));
    }

    [Fact]
    public void Trailing_whitespace_is_trimmed()
    {
        Assert.Equal("hallo", PasteSanitizer.Sanitize("hallo   \n\n"));
    }
}
