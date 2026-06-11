using System.Text;
using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class DpapiSecretProtectorTests
{
    [Fact]
    public void Protect_then_unprotect_round_trips_to_the_original_bytes()
    {
        // DPAPI is a Windows-only OS service; the acceptance check runs on the
        // Windows side, but guard so the suite still loads cross-platform.
        if (!OperatingSystem.IsWindows()) return;

        var protector = new DpapiSecretProtector();
        var plaintext = Encoding.UTF8.GetBytes("sk-live-deadbeef-1234567890");

        var cipher = protector.Protect(plaintext);
        var recovered = protector.Unprotect(cipher);

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void Protected_bytes_are_not_the_plaintext()
    {
        if (!OperatingSystem.IsWindows()) return;

        var protector = new DpapiSecretProtector();
        var plaintext = Encoding.UTF8.GetBytes("sk-live-deadbeef-1234567890");

        var cipher = protector.Protect(plaintext);

        Assert.NotEqual(plaintext, cipher);
        Assert.DoesNotContain("sk-live", Encoding.UTF8.GetString(cipher));
    }
}
