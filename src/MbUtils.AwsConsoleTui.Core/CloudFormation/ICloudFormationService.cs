using MbUtils.AwsConsoleTui.Core.Models;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public interface ICloudFormationService
{
    Task<IReadOnlyList<StackInfo>> ListStacksAsync(CancellationToken ct);
}
