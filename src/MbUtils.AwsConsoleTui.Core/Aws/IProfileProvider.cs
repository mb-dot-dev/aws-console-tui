namespace MbUtils.AwsConsoleTui.Core.Aws;

public interface IProfileProvider
{
    IReadOnlyList<string> GetProfileNames();
    IReadOnlyList<string> GetRegions();
    string? DefaultProfile { get; }
    string? DefaultRegion { get; }
}
