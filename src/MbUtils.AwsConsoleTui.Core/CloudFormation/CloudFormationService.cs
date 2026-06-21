using MbUtils.AwsConsoleTui.Core.Models;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public sealed class CloudFormationService : ICloudFormationService
{
    private readonly ICloudFormationClient _client;

    public CloudFormationService(ICloudFormationClient client) => _client = client;

    public async Task<IReadOnlyList<StackInfo>> ListStacksAsync(CancellationToken ct)
    {
        var result = new List<StackInfo>();
        await foreach (var stack in _client.DescribeAllStacksAsync(ct))
        {
            result.Add(StackMapper.ToStackInfo(stack));
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }
}
