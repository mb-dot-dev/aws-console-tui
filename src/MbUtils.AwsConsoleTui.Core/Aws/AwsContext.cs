namespace MbUtils.AwsConsoleTui.Core.Aws;

public sealed class AwsContext : IAwsContext
{
    public string ProfileName { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;

    public void Set(string profileName, string region)
    {
        ProfileName = profileName;
        Region = region;
    }
}
