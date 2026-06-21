using Amazon.CloudFormation.Model;
using MbUtils.AwsConsoleTui.Core.Models;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public static class StackMapper
{
    public static StackInfo ToStackInfo(Stack stack) =>
        new(
            Name: stack.StackName ?? string.Empty,
            Status: stack.StackStatus?.Value ?? string.Empty,
            CreatedAt: stack.CreationTime ?? default,
            LastUpdatedAt: stack.LastUpdatedTime,
            Description: stack.Description);
}
