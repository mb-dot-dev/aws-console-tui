using System.Runtime.CompilerServices;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.CloudFormation;

public class CloudFormationServiceTests
{
    private sealed class FakeCloudFormationClient : ICloudFormationClient
    {
        private readonly IReadOnlyList<Stack> _stacks;
        public FakeCloudFormationClient(IReadOnlyList<Stack> stacks) => _stacks = stacks;

        public async IAsyncEnumerable<Stack> DescribeAllStacksAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var s in _stacks)
            {
                ct.ThrowIfCancellationRequested();
                yield return s;
            }
            await Task.CompletedTask;
        }
    }

    private static Stack MakeStack(string name) => new()
    {
        StackName = name,
        StackStatus = StackStatus.CREATE_COMPLETE,
        CreationTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task ListStacks_MapsAndAggregatesAllPages()
    {
        var client = new FakeCloudFormationClient(new[]
        {
            MakeStack("one"), MakeStack("two"), MakeStack("three"),
        });
        var service = new CloudFormationService(client);

        var result = await service.ListStacksAsync(CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal("CREATE_COMPLETE", s.Status));
        Assert.Equal(new[] { "one", "three", "two" }, result.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task ListStacks_SortsByNameCaseInsensitive()
    {
        var client = new FakeCloudFormationClient(new[]
        {
            MakeStack("zebra"), MakeStack("Apple"), MakeStack("mango"),
        });
        var service = new CloudFormationService(client);

        var result = await service.ListStacksAsync(CancellationToken.None);

        Assert.Equal(new[] { "Apple", "mango", "zebra" }, result.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task ListStacks_EmptyClient_ReturnsEmpty()
    {
        var service = new CloudFormationService(new FakeCloudFormationClient(Array.Empty<Stack>()));

        var result = await service.ListStacksAsync(CancellationToken.None);

        Assert.Empty(result);
    }
}
