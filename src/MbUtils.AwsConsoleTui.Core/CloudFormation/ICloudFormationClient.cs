using Amazon.CloudFormation.Model;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public interface ICloudFormationClient
{
    IAsyncEnumerable<Stack> DescribeAllStacksAsync(CancellationToken ct);
}
