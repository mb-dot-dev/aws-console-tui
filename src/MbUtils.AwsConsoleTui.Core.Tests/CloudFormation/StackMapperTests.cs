using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.CloudFormation;

public class StackMapperTests
{
    [Fact]
    public void MapsAllFields_WhenPresent()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var updated = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var stack = new Stack
        {
            StackName = "my-stack",
            StackStatus = StackStatus.CREATE_COMPLETE,
            CreationTime = created,
            LastUpdatedTime = updated,
            Description = "a description",
        };

        var info = StackMapper.ToStackInfo(stack);

        Assert.Equal("my-stack", info.Name);
        Assert.Equal("CREATE_COMPLETE", info.Status);
        Assert.Equal(created, info.CreatedAt);
        Assert.Equal(updated, info.LastUpdatedAt);
        Assert.Equal("a description", info.Description);
    }

    [Fact]
    public void MapsNullLastUpdatedAndDescription()
    {
        var stack = new Stack
        {
            StackName = "fresh-stack",
            StackStatus = StackStatus.CREATE_COMPLETE,
            CreationTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            // LastUpdatedTime and Description left unset
        };

        var info = StackMapper.ToStackInfo(stack);

        Assert.Null(info.LastUpdatedAt);
        Assert.Null(info.Description);
    }
}
