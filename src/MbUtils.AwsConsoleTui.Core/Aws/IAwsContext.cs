namespace MbUtils.AwsConsoleTui.Core.Aws;

public interface IAwsContext
{
    string ProfileName { get; }
    string Region { get; }
    void Set(string profileName, string region);
}
