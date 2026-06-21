using MbUtils.AwsConsoleTui.Core.CloudFormation;
using MbUtils.AwsConsoleTui.Core.Models;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.CloudFormation;

public class StackFilterTests
{
    private static readonly IReadOnlyList<StackInfo> Stacks = new[]
    {
        new StackInfo("alpha-prod", "CREATE_COMPLETE", DateTime.UtcNow, null, null),
        new StackInfo("Beta-Prod", "UPDATE_COMPLETE", DateTime.UtcNow, null, null),
        new StackInfo("gamma-dev", "CREATE_COMPLETE", DateTime.UtcNow, null, null),
    };

    [Fact]
    public void EmptyFilter_ReturnsAll()
    {
        var result = StackFilter.Apply(Stacks, "");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void WhitespaceFilter_ReturnsAll()
    {
        var result = StackFilter.Apply(Stacks, "   ");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void NullFilter_ReturnsAll()
    {
        var result = StackFilter.Apply(Stacks, null);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Substring_IsCaseInsensitive()
    {
        var result = StackFilter.Apply(Stacks, "prod");
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Name == "alpha-prod");
        Assert.Contains(result, s => s.Name == "Beta-Prod");
    }

    [Fact]
    public void Filter_IsTrimmedBeforeMatching()
    {
        var result = StackFilter.Apply(Stacks, "  gamma  ");
        Assert.Single(result);
        Assert.Equal("gamma-dev", result[0].Name);
    }

    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var result = StackFilter.Apply(Stacks, "zzz");
        Assert.Empty(result);
    }
}
