using Amazon.CloudFormation;

namespace MbUtils.AwsConsoleTui.Core.Aws;

public interface IAwsClientFactory
{
    IAmazonCloudFormation CreateCloudFormationClient();
}
