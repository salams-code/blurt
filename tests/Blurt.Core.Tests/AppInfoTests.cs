using Blurt.Core;
using Xunit;

namespace Blurt.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_is_Blurt()
    {
        Assert.Equal("Blurt", AppInfo.Name);
    }
}
