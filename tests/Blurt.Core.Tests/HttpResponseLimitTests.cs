using System.Text;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class HttpResponseLimitTests
{
    [Fact]
    public async Task Reads_a_body_within_the_limit()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var result = await HttpResponseLimit.ReadAsStringAsync(stream, maxBytes: 100);

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task Multibyte_utf8_is_decoded_correctly()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("schöne Grüße"));

        var result = await HttpResponseLimit.ReadAsStringAsync(stream, maxBytes: 100);

        Assert.Equal("schöne Grüße", result);
    }

    [Fact]
    public async Task A_body_exactly_at_the_limit_is_accepted()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', 100));
        using var stream = new MemoryStream(bytes);

        var result = await HttpResponseLimit.ReadAsStringAsync(stream, maxBytes: 100);

        Assert.Equal(100, result.Length);
    }

    [Fact]
    public async Task A_body_over_the_limit_is_rejected()
    {
        // The paste-bomb / OOM guard: a provider returning far more than expected
        // must fail fast rather than buffer it all.
        var bytes = Encoding.UTF8.GetBytes(new string('x', 1000));
        using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => HttpResponseLimit.ReadAsStringAsync(stream, maxBytes: 100));
    }
}
