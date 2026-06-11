using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class AsyncLazyTests
{
    [Fact]
    public async Task A_successful_value_is_created_once_and_reused()
    {
        var factoryCalls = 0;
        var lazy = new AsyncLazy<string>(() =>
        {
            factoryCalls++;
            return Task.FromResult("transcriber");
        });

        var first = await lazy.GetAsync();
        var second = await lazy.GetAsync();

        Assert.Equal("transcriber", first);
        Assert.Equal("transcriber", second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task A_failed_attempt_is_not_cached_so_the_next_get_retries()
    {
        var factoryCalls = 0;
        var lazy = new AsyncLazy<string>(() => factoryCalls++ == 0
            ? Task.FromException<string>(new InvalidOperationException("network down"))
            : Task.FromResult("transcriber"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => lazy.GetAsync());
        var retried = await lazy.GetAsync();

        Assert.Equal("transcriber", retried);
        Assert.Equal(2, factoryCalls);
    }
}
