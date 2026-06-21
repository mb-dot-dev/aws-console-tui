using Amazon;
using Amazon.Runtime.CredentialManagement;

namespace MbUtils.AwsConsoleTui.Core.Aws;

public sealed class ProfileProvider : IProfileProvider
{
    private readonly Func<string, string?> _env;

    public ProfileProvider(Func<string, string?>? envReader = null)
        => _env = envReader ?? Environment.GetEnvironmentVariable;

    public IReadOnlyList<string> GetProfileNames()
    {
        var chain = new CredentialProfileStoreChain();
        return chain.ListProfiles().Select(p => p.Name).ToList();
    }

    public IReadOnlyList<string> GetRegions()
        => RegionEndpoint.EnumerableAllRegions
            .Select(r => r.SystemName)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

    public string? DefaultProfile => _env("AWS_PROFILE");

    public string? DefaultRegion => _env("AWS_REGION") ?? _env("AWS_DEFAULT_REGION");
}
