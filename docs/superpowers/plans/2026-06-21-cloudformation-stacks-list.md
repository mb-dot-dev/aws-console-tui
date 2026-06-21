# CloudFormation Stacks List (v1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Terminal.Gui-based AWS Console TUI whose first feature is a read-only, filterable, refreshable list of CloudFormation stacks, on a clean Core/UI/Tests structure that scales to future services.

**Architecture:** A `Core` class library holds plain domain models and AWS access behind interfaces (so it is testable with hand-written fakes and free of any UI dependency). The existing `ConsoleApp` holds only Terminal.Gui v2 UI and constructs everything in `Program.cs` (no DI container — YAGNI). A `Core.Tests` xUnit project tests the pure logic. Async AWS calls run off the UI thread and marshal results back with `Application.Invoke`.

**Tech Stack:** .NET 10, C# (nullable enabled), Terminal.Gui 2.4.7 (v2 API), AWSSDK.CloudFormation (v4), xUnit.

## Global Constraints

- Target framework: `net10.0` for all projects.
- `Terminal.Gui` version `2.4.7` (v2 API) — ConsoleApp only.
- `AWSSDK.CloudFormation` v4 — Core only. The UI and tests must not reference the AWS SDK directly; all AWS access goes through Core interfaces.
- `Nullable` enabled and `ImplicitUsings` enabled in every project.
- v1 is strictly read-only — no create/update/delete AWS calls anywhere.
- The Terminal.Gui UI layer is NOT unit-tested. Only `Core` logic has tests.
- Live filtering is in-memory only — it must never trigger an AWS API call.

**Note on Terminal.Gui v2:** The v2 API (2.4.7) differs from v1. The UI code below is written against v2 in good faith; if the build surfaces a signature difference, adjust to the actual 2.4.7 API (confirm via build errors / IntelliSense) — the intent and structure stay the same.

---

### Task 1: Scaffold Core and Core.Tests projects

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.Core/MbUtils.AwsConsoleTui.Core.csproj`
- Create: `src/MbUtils.AwsConsoleTui.Core.Tests/MbUtils.AwsConsoleTui.Core.Tests.csproj`
- Modify: `MbUtils.AwsConsoleTui.slnx`
- Delete: the default `Class1.cs` / `UnitTest1.cs` that the templates generate

**Interfaces:**
- Consumes: nothing.
- Produces: a buildable 3-project solution. `Core` references `AWSSDK.CloudFormation`. `ConsoleApp` and `Core.Tests` reference `Core`.

- [ ] **Step 1: Create the two projects**

```bash
dotnet new classlib -n MbUtils.AwsConsoleTui.Core -o src/MbUtils.AwsConsoleTui.Core -f net10.0
dotnet new xunit -n MbUtils.AwsConsoleTui.Core.Tests -o src/MbUtils.AwsConsoleTui.Core.Tests -f net10.0
rm src/MbUtils.AwsConsoleTui.Core/Class1.cs
rm src/MbUtils.AwsConsoleTui.Core.Tests/UnitTest1.cs
```

- [ ] **Step 2: Add references and the AWS SDK package**

```bash
dotnet add src/MbUtils.AwsConsoleTui.Core package AWSSDK.CloudFormation
dotnet add src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj reference src/MbUtils.AwsConsoleTui.Core/MbUtils.AwsConsoleTui.Core.csproj
dotnet add src/MbUtils.AwsConsoleTui.Core.Tests/MbUtils.AwsConsoleTui.Core.Tests.csproj reference src/MbUtils.AwsConsoleTui.Core/MbUtils.AwsConsoleTui.Core.csproj
```

- [ ] **Step 3: Ensure Core csproj has nullable + implicit usings**

Confirm `src/MbUtils.AwsConsoleTui.Core/MbUtils.AwsConsoleTui.Core.csproj` contains:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.CloudFormation" Version="4.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Register both projects in the solution**

Replace `MbUtils.AwsConsoleTui.slnx` with:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj" />
    <Project Path="src/MbUtils.AwsConsoleTui.Core/MbUtils.AwsConsoleTui.Core.csproj" />
    <Project Path="src/MbUtils.AwsConsoleTui.Core.Tests/MbUtils.AwsConsoleTui.Core.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 5: Build and run the (empty) test suite**

Run: `dotnet build && dotnet test`
Expected: build succeeds; test run reports `Passed!  - Failed: 0, Passed: 0` (no tests yet).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold Core library and Core.Tests projects"
```

---

### Task 2: StackInfo model and StackFilter

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.Core/Models/StackInfo.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/CloudFormation/StackFilter.cs`
- Test: `src/MbUtils.AwsConsoleTui.Core.Tests/CloudFormation/StackFilterTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `MbUtils.AwsConsoleTui.Core.Models.StackInfo` — `record(string Name, string Status, DateTime CreatedAt, DateTime? LastUpdatedAt, string? Description)`.
  - `MbUtils.AwsConsoleTui.Core.CloudFormation.StackFilter.Apply(IReadOnlyList<StackInfo> stacks, string? nameFilter) -> IReadOnlyList<StackInfo>`.

- [ ] **Step 1: Create the StackInfo model**

`src/MbUtils.AwsConsoleTui.Core/Models/StackInfo.cs`:

```csharp
namespace MbUtils.AwsConsoleTui.Core.Models;

public sealed record StackInfo(
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt,
    string? Description);
```

- [ ] **Step 2: Write the failing StackFilter tests**

`src/MbUtils.AwsConsoleTui.Core.Tests/CloudFormation/StackFilterTests.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using MbUtils.AwsConsoleTui.Core.Models;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.CloudFormation;

public class StackFilterTests
{
    private static readonly IReadOnlyList<StackInfo> Stacks = new[]
    {
        new StackInfo("alpha-prod", "CREATE_COMPLETE", DateTime.UtcNow, null, null),
        new StackInfo("Beta-Prod", "UPDATE_COMPLETE", DateTime.UtcNow, null, null),
        new StackInfo("gamma-dev", "CREATE_COMPLETE", DateTime.UtcNow, null, null),
    };

    [Fact]
    public void EmptyFilter_ReturnsAll()
    {
        var result = StackFilter.Apply(Stacks, "");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void WhitespaceFilter_ReturnsAll()
    {
        var result = StackFilter.Apply(Stacks, "   ");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void NullFilter_ReturnsAll()
    {
        var result = StackFilter.Apply(Stacks, null);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Substring_IsCaseInsensitive()
    {
        var result = StackFilter.Apply(Stacks, "prod");
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Name == "alpha-prod");
        Assert.Contains(result, s => s.Name == "Beta-Prod");
    }

    [Fact]
    public void Filter_IsTrimmedBeforeMatching()
    {
        var result = StackFilter.Apply(Stacks, "  gamma  ");
        Assert.Single(result);
        Assert.Equal("gamma-dev", result[0].Name);
    }

    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var result = StackFilter.Apply(Stacks, "zzz");
        Assert.Empty(result);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~StackFilterTests"`
Expected: FAIL — `StackFilter` does not exist (compile error).

- [ ] **Step 4: Implement StackFilter**

`src/MbUtils.AwsConsoleTui.Core/CloudFormation/StackFilter.cs`:

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~StackFilterTests"`
Expected: PASS — 6 passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add StackInfo model and StackFilter"
```

---

### Task 3: Map AWS SDK Stack to StackInfo

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.Core/CloudFormation/StackMapper.cs`
- Test: `src/MbUtils.AwsConsoleTui.Core.Tests/CloudFormation/StackMapperTests.cs`

**Interfaces:**
- Consumes: `StackInfo` (Task 2); `Amazon.CloudFormation.Model.Stack`, `Amazon.CloudFormation.StackStatus` (AWS SDK).
- Produces: `MbUtils.AwsConsoleTui.Core.CloudFormation.StackMapper.ToStackInfo(Amazon.CloudFormation.Model.Stack stack) -> StackInfo`.

**Note:** AWSSDK v4 exposes value-type properties as nullable (`DateTime?`) and status as a `StackStatus` constant class with a `.Value` string. If the build shows a property is non-nullable, drop the corresponding `?? default` / `?.`.

- [ ] **Step 1: Write the failing mapper tests**

`src/MbUtils.AwsConsoleTui.Core.Tests/CloudFormation/StackMapperTests.cs`:

```csharp
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.CloudFormation;

public class StackMapperTests
{
    [Fact]
    public void MapsAllFields_WhenPresent()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var updated = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var stack = new Stack
        {
            StackName = "my-stack",
            StackStatus = StackStatus.CREATE_COMPLETE,
            CreationTime = created,
            LastUpdatedTime = updated,
            Description = "a description",
        };

        var info = StackMapper.ToStackInfo(stack);

        Assert.Equal("my-stack", info.Name);
        Assert.Equal("CREATE_COMPLETE", info.Status);
        Assert.Equal(created, info.CreatedAt);
        Assert.Equal(updated, info.LastUpdatedAt);
        Assert.Equal("a description", info.Description);
    }

    [Fact]
    public void MapsNullLastUpdatedAndDescription()
    {
        var stack = new Stack
        {
            StackName = "fresh-stack",
            StackStatus = StackStatus.CREATE_COMPLETE,
            CreationTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            // LastUpdatedTime and Description left unset
        };

        var info = StackMapper.ToStackInfo(stack);

        Assert.Null(info.LastUpdatedAt);
        Assert.Null(info.Description);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~StackMapperTests"`
Expected: FAIL — `StackMapper` does not exist (compile error).

- [ ] **Step 3: Implement StackMapper**

`src/MbUtils.AwsConsoleTui.Core/CloudFormation/StackMapper.cs`:

```csharp
using Amazon.CloudFormation.Model;
using MbUtils.AwsConsoleTui.Core.Models;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public static class StackMapper
{
    public static StackInfo ToStackInfo(Stack stack) =>
        new(
            Name: stack.StackName ?? string.Empty,
            Status: stack.StackStatus?.Value ?? string.Empty,
            CreatedAt: stack.CreationTime ?? default,
            LastUpdatedAt: stack.LastUpdatedTime,
            Description: stack.Description);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~StackMapperTests"`
Expected: PASS — 2 passed.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add StackMapper for SDK Stack to StackInfo"
```

---

### Task 4: CloudFormation client abstraction and service

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.Core/CloudFormation/ICloudFormationClient.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/CloudFormation/ICloudFormationService.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/CloudFormation/CloudFormationService.cs`
- Test: `src/MbUtils.AwsConsoleTui.Core.Tests/CloudFormation/CloudFormationServiceTests.cs`

**Interfaces:**
- Consumes: `StackInfo` (Task 2), `StackMapper` (Task 3), `Amazon.CloudFormation.Model.Stack`.
- Produces:
  - `ICloudFormationClient.DescribeAllStacksAsync(CancellationToken ct) -> IAsyncEnumerable<Amazon.CloudFormation.Model.Stack>`.
  - `ICloudFormationService.ListStacksAsync(CancellationToken ct) -> Task<IReadOnlyList<StackInfo>>`.
  - `CloudFormationService(ICloudFormationClient client)` — aggregates all pages, maps via `StackMapper`, returns the list sorted by name (case-insensitive ascending).

- [ ] **Step 1: Create the two interfaces**

`src/MbUtils.AwsConsoleTui.Core/CloudFormation/ICloudFormationClient.cs`:

```csharp
using Amazon.CloudFormation.Model;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public interface ICloudFormationClient
{
    IAsyncEnumerable<Stack> DescribeAllStacksAsync(CancellationToken ct);
}
```

`src/MbUtils.AwsConsoleTui.Core/CloudFormation/ICloudFormationService.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.Models;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public interface ICloudFormationService
{
    Task<IReadOnlyList<StackInfo>> ListStacksAsync(CancellationToken ct);
}
```

- [ ] **Step 2: Write the failing service tests**

`src/MbUtils.AwsConsoleTui.Core.Tests/CloudFormation/CloudFormationServiceTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CloudFormationServiceTests"`
Expected: FAIL — `CloudFormationService` does not exist (compile error).

- [ ] **Step 4: Implement CloudFormationService**

`src/MbUtils.AwsConsoleTui.Core/CloudFormation/CloudFormationService.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.Models;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public sealed class CloudFormationService : ICloudFormationService
{
    private readonly ICloudFormationClient _client;

    public CloudFormationService(ICloudFormationClient client) => _client = client;

    public async Task<IReadOnlyList<StackInfo>> ListStacksAsync(CancellationToken ct)
    {
        var result = new List<StackInfo>();
        await foreach (var stack in _client.DescribeAllStacksAsync(ct))
        {
            result.Add(StackMapper.ToStackInfo(stack));
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CloudFormationServiceTests"`
Expected: PASS — 3 passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add CloudFormation client abstraction and service"
```

---

### Task 5: AWS context, profile provider, client factory, real client adapter

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.Core/Aws/IAwsContext.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/Aws/AwsContext.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/Aws/IProfileProvider.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/Aws/ProfileProvider.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/Aws/IAwsClientFactory.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/Aws/AwsClientFactory.cs`
- Create: `src/MbUtils.AwsConsoleTui.Core/CloudFormation/CloudFormationClient.cs`
- Test: `src/MbUtils.AwsConsoleTui.Core.Tests/Aws/AwsContextTests.cs`
- Test: `src/MbUtils.AwsConsoleTui.Core.Tests/Aws/ProfileProviderTests.cs`

**Interfaces:**
- Consumes: `ICloudFormationClient` (Task 4), AWS SDK types.
- Produces:
  - `IAwsContext` — `string ProfileName { get; }`, `string Region { get; }`, `void Set(string profileName, string region)`.
  - `AwsContext` — concrete `IAwsContext`, both properties start as `""`.
  - `IProfileProvider` — `IReadOnlyList<string> GetProfileNames()`, `IReadOnlyList<string> GetRegions()`, `string? DefaultProfile { get; }`, `string? DefaultRegion { get; }`.
  - `ProfileProvider(Func<string, string?>? envReader = null)`.
  - `IAwsClientFactory.CreateCloudFormationClient() -> Amazon.CloudFormation.IAmazonCloudFormation`.
  - `AwsClientFactory(IAwsContext context)`.
  - `CloudFormationClient(Amazon.CloudFormation.IAmazonCloudFormation client)` — concrete `ICloudFormationClient` wrapping the SDK `DescribeStacks` paginator.

- [ ] **Step 1: Write the failing AwsContext tests**

`src/MbUtils.AwsConsoleTui.Core.Tests/Aws/AwsContextTests.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.Aws;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.Aws;

public class AwsContextTests
{
    [Fact]
    public void StartsEmpty()
    {
        var ctx = new AwsContext();
        Assert.Equal(string.Empty, ctx.ProfileName);
        Assert.Equal(string.Empty, ctx.Region);
    }

    [Fact]
    public void Set_UpdatesProfileAndRegion()
    {
        var ctx = new AwsContext();
        ctx.Set("dev", "eu-west-1");
        Assert.Equal("dev", ctx.ProfileName);
        Assert.Equal("eu-west-1", ctx.Region);
    }
}
```

- [ ] **Step 2: Write the failing ProfileProvider tests**

`src/MbUtils.AwsConsoleTui.Core.Tests/Aws/ProfileProviderTests.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.Aws;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.Aws;

public class ProfileProviderTests
{
    [Fact]
    public void GetRegions_IsNonEmptyAndContainsUsEast1()
    {
        var provider = new ProfileProvider(_ => null);
        var regions = provider.GetRegions();
        Assert.NotEmpty(regions);
        Assert.Contains("us-east-1", regions);
    }

    [Fact]
    public void GetRegions_IsSortedAscending()
    {
        var provider = new ProfileProvider(_ => null);
        var regions = provider.GetRegions();
        Assert.Equal(regions.OrderBy(r => r, StringComparer.Ordinal).ToArray(), regions.ToArray());
    }

    [Fact]
    public void DefaultRegion_PrefersAwsRegionEnv()
    {
        var provider = new ProfileProvider(name => name == "AWS_REGION" ? "ap-southeast-2" : null);
        Assert.Equal("ap-southeast-2", provider.DefaultRegion);
    }

    [Fact]
    public void DefaultRegion_FallsBackToAwsDefaultRegionEnv()
    {
        var provider = new ProfileProvider(name => name == "AWS_DEFAULT_REGION" ? "us-west-2" : null);
        Assert.Equal("us-west-2", provider.DefaultRegion);
    }

    [Fact]
    public void DefaultProfile_ReadsAwsProfileEnv()
    {
        var provider = new ProfileProvider(name => name == "AWS_PROFILE" ? "work" : null);
        Assert.Equal("work", provider.DefaultProfile);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~Aws."`
Expected: FAIL — `AwsContext` / `ProfileProvider` do not exist (compile error).

- [ ] **Step 4: Implement IAwsContext and AwsContext**

`src/MbUtils.AwsConsoleTui.Core/Aws/IAwsContext.cs`:

```csharp
namespace MbUtils.AwsConsoleTui.Core.Aws;

public interface IAwsContext
{
    string ProfileName { get; }
    string Region { get; }
    void Set(string profileName, string region);
}
```

`src/MbUtils.AwsConsoleTui.Core/Aws/AwsContext.cs`:

```csharp
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
```

- [ ] **Step 5: Implement IProfileProvider and ProfileProvider**

`src/MbUtils.AwsConsoleTui.Core/Aws/IProfileProvider.cs`:

```csharp
namespace MbUtils.AwsConsoleTui.Core.Aws;

public interface IProfileProvider
{
    IReadOnlyList<string> GetProfileNames();
    IReadOnlyList<string> GetRegions();
    string? DefaultProfile { get; }
    string? DefaultRegion { get; }
}
```

`src/MbUtils.AwsConsoleTui.Core/Aws/ProfileProvider.cs`:

```csharp
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
        return chain.ListProfileNames();
    }

    public IReadOnlyList<string> GetRegions()
        => RegionEndpoint.EnumerableAllRegions
            .Select(r => r.SystemName)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

    public string? DefaultProfile => _env("AWS_PROFILE");

    public string? DefaultRegion => _env("AWS_REGION") ?? _env("AWS_DEFAULT_REGION");
}
```

- [ ] **Step 6: Implement IAwsClientFactory and AwsClientFactory**

`src/MbUtils.AwsConsoleTui.Core/Aws/IAwsClientFactory.cs`:

```csharp
using Amazon.CloudFormation;

namespace MbUtils.AwsConsoleTui.Core.Aws;

public interface IAwsClientFactory
{
    IAmazonCloudFormation CreateCloudFormationClient();
}
```

`src/MbUtils.AwsConsoleTui.Core/Aws/AwsClientFactory.cs`:

```csharp
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
```

- [ ] **Step 7: Implement the real CloudFormationClient adapter**

`src/MbUtils.AwsConsoleTui.Core/CloudFormation/CloudFormationClient.cs`:

```csharp
using System.Runtime.CompilerServices;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace MbUtils.AwsConsoleTui.Core.CloudFormation;

public sealed class CloudFormationClient : ICloudFormationClient
{
    private readonly IAmazonCloudFormation _client;

    public CloudFormationClient(IAmazonCloudFormation client) => _client = client;

    public async IAsyncEnumerable<Stack> DescribeAllStacksAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var paginator = _client.Paginators.DescribeStacks(new DescribeStacksRequest());
        await foreach (var stack in paginator.Stacks.WithCancellation(ct))
        {
            yield return stack;
        }
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~Aws."`
Expected: PASS — 7 passed.

- [ ] **Step 9: Build the whole solution**

Run: `dotnet build`
Expected: build succeeds (confirms the SDK-touching factory and adapter compile).

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: add AWS context, profile provider, client factory and adapter"
```

---

### Task 6: ConsoleApp shell — startup dialog, menu, status bar

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/ProfileRegionDialog.cs`
- Create: `src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/AppMenu.cs`
- Create: `src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/AppStatusBar.cs`
- Modify: `src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs` (full replace)

**Interfaces:**
- Consumes: `IProfileProvider`, `ProfileProvider`, `AwsContext`, `AwsClientFactory`, `CloudFormationService`, `CloudFormationClient` (Tasks 4–5). Also `StacksView` is referenced here but created in Task 7 — this task leaves a placeholder content `Window` and Task 7 swaps it in.
- Produces:
  - `ProfileRegionDialog.Show(IProfileProvider provider) -> (string? Profile, string? Region)` — returns `(null, null)` if cancelled.
  - `AppMenu.Build() -> MenuBar` — top menu with one active item (`CloudFormation ▸ Stacks`, wired by Task 7) and disabled placeholders.
  - `AppStatusBar.Build(IAwsContext context) -> StatusBar`.

- [ ] **Step 1: Implement the profile/region dialog**

`src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/ProfileRegionDialog.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.Aws;
using Terminal.Gui;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public static class ProfileRegionDialog
{
    public static (string? Profile, string? Region) Show(IProfileProvider provider)
    {
        var profiles = provider.GetProfileNames();
        var regions = provider.GetRegions();

        if (profiles.Count == 0)
        {
            MessageBox.ErrorQuery("No AWS profiles", "No named profiles found in your AWS config.", "OK");
            return (null, null);
        }

        var dialog = new Dialog
        {
            Title = "Select AWS Profile and Region",
            Width = 60,
            Height = 18,
        };

        var profileLabel = new Label { Text = "Profile:", X = 1, Y = 0 };
        var profileList = new ListView
        {
            X = 1, Y = 1, Width = 26, Height = 12,
            Source = new ListWrapper<string>(new(profiles)),
        };

        var regionLabel = new Label { Text = "Region:", X = 30, Y = 0 };
        var regionList = new ListView
        {
            X = 30, Y = 1, Width = 26, Height = 12,
            Source = new ListWrapper<string>(new(regions)),
        };

        SelectDefault(profileList, profiles, provider.DefaultProfile);
        SelectDefault(regionList, regions, provider.DefaultRegion);

        (string? Profile, string? Region) result = (null, null);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            result = (profiles[profileList.SelectedItem], regions[regionList.SelectedItem]);
            Application.RequestStop();
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop();

        dialog.Add(profileLabel, profileList, regionLabel, regionList);
        dialog.AddButton(ok);
        dialog.AddButton(cancel);

        Application.Run(dialog);
        dialog.Dispose();
        return result;
    }

    private static void SelectDefault(ListView list, IReadOnlyList<string> items, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var index = items.ToList().FindIndex(i => string.Equals(i, value, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            list.SelectedItem = index;
        }
    }
}
```

- [ ] **Step 2: Implement the menu with disabled placeholders**

`src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/AppMenu.cs`:

```csharp
using Terminal.Gui;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public static class AppMenu
{
    // onShowStacks is wired by Task 7; null keeps the item inert until then.
    public static MenuBar Build(Action? onShowStacks = null)
    {
        var disabled = new Func<bool>(() => false);

        MenuItem Placeholder(string title) => new(title, "Coming soon", () => { }) { CanExecute = disabled };

        var stacksItem = new MenuItem("_Stacks", "List CloudFormation stacks", () => onShowStacks?.Invoke())
        {
            CanExecute = () => onShowStacks is not null,
        };

        return new MenuBar
        {
            Menus =
            [
                new MenuBarItem("_File",
                [
                    new MenuItem("_Quit", "Exit the application", () => Application.RequestStop()),
                ]),
                new MenuBarItem("_CloudFormation",
                [
                    stacksItem,
                    Placeholder("Stack _Details"),
                ]),
                new MenuBarItem("_S3",
                [
                    Placeholder("_Buckets"),
                ]),
                new MenuBarItem("_Lambda",
                [
                    Placeholder("_Functions"),
                ]),
                new MenuBarItem("S_QS",
                [
                    Placeholder("_Queues"),
                ]),
            ],
        };
    }
}
```

- [ ] **Step 3: Implement the status bar**

`src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/AppStatusBar.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.Aws;
using Terminal.Gui;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public static class AppStatusBar
{
    public static StatusBar Build(IAwsContext context)
    {
        return new StatusBar(
        [
            new Shortcut(Key.Empty, $"Profile: {context.ProfileName}", null),
            new Shortcut(Key.Empty, $"Region: {context.Region}", null),
            new Shortcut(Key.F5, "Refresh", null),
            new Shortcut(Key.Q.WithCtrl, "Quit", () => Application.RequestStop()),
        ]);
    }
}
```

- [ ] **Step 4: Replace Program.cs with the composition root**

`src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs`:

```csharp
using MbUtils.AwsConsoleTui.ConsoleApp.Ui;
using MbUtils.AwsConsoleTui.Core.Aws;
using Terminal.Gui;

Application.Init();
try
{
    var profileProvider = new ProfileProvider();
    var (profile, region) = ProfileRegionDialog.Show(profileProvider);
    if (profile is null || region is null)
    {
        return;
    }

    var context = new AwsContext();
    context.Set(profile, region);

    var menu = AppMenu.Build();
    var status = AppStatusBar.Build(context);

    // Placeholder content; Task 7 replaces this with the StacksView.
    var content = new Window
    {
        Title = "CloudFormation Stacks",
        X = 0,
        Y = Pos.Bottom(menu),
        Width = Dim.Fill(),
        Height = Dim.Fill(1),
    };

    var top = new Toplevel();
    top.Add(menu, content, status);
    Application.Run(top);
    top.Dispose();
}
finally
{
    Application.Shutdown();
}
```

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 6: Manual smoke test**

Run: `dotnet run --project src/MbUtils.AwsConsoleTui.ConsoleApp`
Expected: a profile/region selection dialog appears with two lists and OK/Cancel. After OK, the main screen shows the top menu (with `S3`, `Lambda`, `SQS`, and `Stack Details` greyed-out / non-activatable), an empty "CloudFormation Stacks" window, and a status bar showing the chosen profile and region. `Ctrl-Q` quits. (Requires at least one configured AWS profile; if none, an error dialog appears and the app exits cleanly.)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add TUI shell with profile/region dialog, menu and status bar"
```

---

### Task 7: Stacks view — table, filter, spinner, async load, error handling

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/StacksView.cs`
- Modify: `src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs` (wire StacksView into the shell + menu)

**Interfaces:**
- Consumes: `ICloudFormationService` (Task 4), `StackFilter`, `StackInfo` (Task 2), `CloudFormationService` + `CloudFormationClient` + `AwsClientFactory` (Tasks 4–5), `AppMenu.Build(Action?)` (Task 6).
- Produces: `StacksView : View` with constructor `StacksView(Func<ICloudFormationService> serviceFactory)` and a public `void Reload()` method (used by `F5` and the menu).

- [ ] **Step 1: Implement StacksView**

`src/MbUtils.AwsConsoleTui.ConsoleApp/Ui/StacksView.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using MbUtils.AwsConsoleTui.Core.Models;
using Terminal.Gui;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public sealed class StacksView : View
{
    private readonly Func<ICloudFormationService> _serviceFactory;
    private readonly TextField _filter;
    private readonly TableView _table;
    private readonly SpinnerView _spinner;
    private readonly Label _statusLabel;

    private IReadOnlyList<StackInfo> _allStacks = Array.Empty<StackInfo>();
    private bool _isLoading;

    public StacksView(Func<ICloudFormationService> serviceFactory)
    {
        _serviceFactory = serviceFactory;
        Width = Dim.Fill();
        Height = Dim.Fill();

        var filterLabel = new Label { Text = "Filter:", X = 0, Y = 0 };
        _filter = new TextField { X = Pos.Right(filterLabel) + 1, Y = 0, Width = 30 };
        _filter.TextChanged += (_, _) => ApplyFilter();

        _spinner = new SpinnerView
        {
            X = Pos.Right(_filter) + 2,
            Y = 0,
            Visible = false,
            AutoSpin = false,
        };

        _statusLabel = new Label { Text = "", X = Pos.Right(_spinner) + 2, Y = 0, Width = Dim.Fill() };

        _table = new TableView
        {
            X = 0,
            Y = Pos.Bottom(_filter) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
        };
        // Row selection seam for a future Stack Details view (intentionally inert in v1).
        _table.CellActivated += (_, _) => { };

        Add(filterLabel, _filter, _spinner, _statusLabel, _table);

        KeyDown += (_, key) =>
        {
            if (key == Key.F5)
            {
                Reload();
                key.Handled = true;
            }
        };
    }

    public void Reload()
    {
        if (_isLoading)
        {
            return;
        }

        SetLoading(true);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var service = _serviceFactory();
            var stacks = await service.ListStacksAsync(CancellationToken.None);
            Application.Invoke(() =>
            {
                _allStacks = stacks;
                ApplyFilter();
                SetLoading(false);
                _statusLabel.Text = $"{_allStacks.Count} stack(s)";
            });
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                SetLoading(false);
                _statusLabel.Text = "Error";
                MessageBox.ErrorQuery("Failed to load stacks", ex.Message, "OK");
            });
        }
    }

    private void ApplyFilter()
    {
        var filtered = StackFilter.Apply(_allStacks, _filter.Text);
        _table.Table = new EnumerableTableSource<StackInfo>(
            filtered,
            new Dictionary<string, Func<StackInfo, object>>
            {
                ["Name"] = s => s.Name,
                ["Status"] = s => s.Status,
                ["Created"] = s => s.CreatedAt.ToString("u"),
                ["Last Updated"] = s => s.LastUpdatedAt?.ToString("u") ?? "-",
                ["Description"] = s => s.Description ?? "-",
            });
        _table.SetNeedsDraw();
    }

    private void SetLoading(bool loading)
    {
        _isLoading = loading;
        _spinner.Visible = loading;
        _spinner.AutoSpin = loading;
        if (loading)
        {
            _statusLabel.Text = "Loading…";
        }
    }
}
```

- [ ] **Step 2: Wire StacksView into Program.cs**

Replace the body of `src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs` between `context.Set(...)` and `Application.Run(top)` so the view is constructed, the menu calls it, and it loads on startup. Full file:

```csharp
using MbUtils.AwsConsoleTui.ConsoleApp.Ui;
using MbUtils.AwsConsoleTui.Core.Aws;
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using Terminal.Gui;

Application.Init();
try
{
    var profileProvider = new ProfileProvider();
    var (profile, region) = ProfileRegionDialog.Show(profileProvider);
    if (profile is null || region is null)
    {
        return;
    }

    var context = new AwsContext();
    context.Set(profile, region);
    var clientFactory = new AwsClientFactory(context);

    ICloudFormationService ServiceFactory() =>
        new CloudFormationService(new CloudFormationClient(clientFactory.CreateCloudFormationClient()));

    var stacksView = new StacksView(ServiceFactory)
    {
        X = 0,
        Width = Dim.Fill(),
    };

    var menu = AppMenu.Build(onShowStacks: stacksView.Reload);
    var status = AppStatusBar.Build(context);

    var content = new Window
    {
        Title = "CloudFormation Stacks",
        X = 0,
        Y = Pos.Bottom(menu),
        Width = Dim.Fill(),
        Height = Dim.Fill(1),
    };
    content.Add(stacksView);

    var top = new Toplevel();
    top.Add(menu, content, status);

    top.Loaded += (_, _) => stacksView.Reload();

    Application.Run(top);
    top.Dispose();
}
finally
{
    Application.Shutdown();
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 4: Run the full Core test suite (regression check)**

Run: `dotnet test`
Expected: PASS — all Core tests (18) pass.

- [ ] **Step 5: Manual end-to-end test**

Run: `dotnet run --project src/MbUtils.AwsConsoleTui.ConsoleApp`
Expected (against a profile/region with CloudFormation access):
- After selecting profile/region, the stacks table loads; while loading, the spinner animates and the status reads `Loading…`.
- Once loaded, the table shows Name / Status / Created / Last Updated / Description sorted by name, and the status shows the count.
- Typing in the filter narrows the rows instantly with no spinner (no API call).
- `F5` reloads (spinner animates again).
- Selecting a row does nothing (the future details seam).
- An invalid/expired profile produces an error dialog and the app stays usable (you can `Session`-switch is future work, but `Ctrl-Q` quits cleanly).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add CloudFormation stacks view with filter, spinner and refresh"
```

---

## Self-Review Notes

- **Spec coverage:**
  - 3-project split → Task 1. ✓
  - `StackInfo` model → Task 2. ✓
  - `IAwsContext` → Task 5. ✓
  - `IAwsClientFactory` → Task 5. ✓
  - `IProfileProvider` (profiles + regions + env defaults) → Task 5. ✓
  - `ICloudFormationService` / `DescribeStacks` paginator / mapping → Tasks 3–5. ✓
  - `StackFilter` (case-insensitive, empty returns all) → Task 2. ✓
  - Client adapter for testability → Task 4 (`ICloudFormationClient` + fake) / Task 5 (real). ✓
  - Startup profile/region dialog → Task 6. ✓
  - MenuBar with active Stacks + disabled placeholders (Stack Details, S3, Lambda, SQS) → Task 6. ✓
  - StatusBar (profile | region | key hints) → Task 6. ✓
  - Stacks view: filter TextField + TableView columns + per-view status → Task 7. ✓
  - SpinnerView load indicator (F5 animates, filter does not) → Task 7. ✓
  - Async off-thread + `Application.Invoke` marshaling + Loading state → Task 7. ✓
  - Row selection wired but inert → Task 7 (`CellActivated` no-op). ✓
  - Error handling via `MessageBox.ErrorQuery` + status bar, app stays usable → Task 7. ✓
  - Read-only only (no mutating calls) → enforced by Global Constraints; no delete/update code anywhere. ✓
  - UI not unit-tested; Core logic tested → Tasks 2–5 tests, Task 7 manual. ✓
- **Type consistency:** `StackInfo` fields, `StackFilter.Apply`, `ICloudFormationClient.DescribeAllStacksAsync`, `ICloudFormationService.ListStacksAsync`, `AwsContext.Set`, `ProfileProvider` members, and `AppMenu.Build(Action?)` are used with identical signatures across tasks.
- **Terminal.Gui v2 caveat:** UI signature details (e.g. `Shortcut`, `MenuItem.CanExecute`, `EnumerableTableSource`, `Accepting`/`CellActivated` events) are written against the 2.4.7 API; if the build reveals a difference, adjust to the actual API — structure and intent are unchanged.
