using MbUtils.AwsConsoleTui.Core.Aws;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.Aws;

public class ProfileProviderTests
{
    [Fact]
    public void GetRegions_IsNonEmptyAndContainsUsEast1()
    {
        var provider = new ProfileProvider(_ => null);
        var regions = provider.GetRegions();
        Assert.NotEmpty(regions);
        Assert.Contains("us-east-1", regions);
    }

    [Fact]
    public void GetRegions_IsSortedAscending()
    {
        var provider = new ProfileProvider(_ => null);
        var regions = provider.GetRegions();
        Assert.Equal(regions.OrderBy(r => r, StringComparer.Ordinal).ToArray(), regions.ToArray());
    }

    [Fact]
    public void DefaultRegion_PrefersAwsRegionEnv()
    {
        var provider = new ProfileProvider(name => name == "AWS_REGION" ? "ap-southeast-2" : null);
        Assert.Equal("ap-southeast-2", provider.DefaultRegion);
    }

    [Fact]
    public void DefaultRegion_FallsBackToAwsDefaultRegionEnv()
    {
        var provider = new ProfileProvider(name => name == "AWS_DEFAULT_REGION" ? "us-west-2" : null);
        Assert.Equal("us-west-2", provider.DefaultRegion);
    }

    [Fact]
    public void DefaultProfile_ReadsAwsProfileEnv()
    {
        var provider = new ProfileProvider(name => name == "AWS_PROFILE" ? "work" : null);
        Assert.Equal("work", provider.DefaultProfile);
    }
}
