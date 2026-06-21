using System.Runtime.CompilerServices;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public sealed class CloudFormationClient : ICloudFormationClient
{
    private readonly IAmazonCloudFormation _client;

    public CloudFormationClient(IAmazonCloudFormation client) => _client = client;

    public async IAsyncEnumerable<Stack> DescribeAllStacksAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var paginator = _client.Paginators.DescribeStacks(new DescribeStacksRequest());
        await foreach (var stack in paginator.Stacks.WithCancellation(ct))
        {
            yield return stack;
        }
    }
}
