using Amazon;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace MbUtils.AwsConsoleTui.Core.Aws;

public sealed class AwsClientFactory : IAwsClientFactory
{
    private readonly IAwsContext _context;

    public AwsClientFactory(IAwsContext context) => _context = context;

    public IAmazonCloudFormation CreateCloudFormationClient()
    {
        var credentials = ResolveCredentials(_context.ProfileName);
        var region = RegionEndpoint.GetBySystemName(_context.Region);
        return new AmazonCloudFormationClient(credentials, region);
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
