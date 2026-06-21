using MbUtils.AwsConsoleTui.Core.Aws;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.Aws;

public class AwsContextTests
{
    [Fact]
    public void StartsEmpty()
    {
        var ctx = new AwsContext();
        Assert.Equal(string.Empty, ctx.ProfileName);
        Assert.Equal(string.Empty, ctx.Region);
    }

    [Fact]
    public void Set_UpdatesProfileAndRegion()
    {
        var ctx = new AwsContext();
        ctx.Set("dev", "eu-west-1");
        Assert.Equal("dev", ctx.ProfileName);
        Assert.Equal("eu-west-1", ctx.Region);
    }
}
