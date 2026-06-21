using MbUtils.AwsConsoleTui.Core.Models;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public static class StackFilter
{
    public static IReadOnlyList<StackInfo> Apply(IReadOnlyList<StackInfo> stacks, string? nameFilter)
    {
        if (string.IsNullOrWhiteSpace(nameFilter))
        {
            return stacks;
        }

        var needle = nameFilter.Trim();
        return stacks
            .Where(s => s.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
