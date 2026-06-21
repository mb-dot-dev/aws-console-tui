namespace MbUtils.AwsConsoleTui.Core.Models;

public sealed record StackInfo(
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt,
    string? Description);
