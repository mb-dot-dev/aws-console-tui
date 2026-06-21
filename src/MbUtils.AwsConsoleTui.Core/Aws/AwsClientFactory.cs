using Amazon;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace MbUtils.AwsConsoleTui.Core.Aws;

public sealed class AwsClientFactory : IAwsClientFactory, IDisposable
{
    private readonly IAwsContext _context;
    private IAmazonCloudFormation? _cloudFormationClient;

    public AwsClientFactory(IAwsContext context) => _context = context;

    public IAmazonCloudFormation GetCloudFormationClient()
    {
        if (_cloudFormationClient is null)
        {
            var credentials = ResolveCredentials(_context.ProfileName);
            var region = RegionEndpoint.GetBySystemName(_context.Region);
            _cloudFormationClient = new AmazonCloudFormationClient(credentials, region);
        }

        return _cloudFormationClient;
    }

    public void Dispose()
    {
        _cloudFormationClient?.Dispose();
        _cloudFormationClient = null;
    }

    private static AWSCredentials ResolveCredentials(string profileName)
    {
        var chain = new CredentialProfileStoreChain();
        if (!string.IsNullOrWhiteSpace(profileName)
            && chain.TryGetAWSCredentials(profileName, out var creds))
        {
            return creds;
        }

        return FallbackCredentialsFactory.GetCredentials();
    }
}
