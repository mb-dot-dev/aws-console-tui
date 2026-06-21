# Final Fix Report — feature/cloudformation-stacks-list

## Fix 1: AmazonCloudFormationClient caching and disposal

### Changes

**`src/MbUtils.AwsConsoleTui.Core/Aws/IAwsClientFactory.cs`**
- Renamed `CreateCloudFormationClient()` → `GetCloudFormationClient()` to reflect cached (not freshly-created) semantics.

**`src/MbUtils.AwsConsoleTui.Core/Aws/AwsClientFactory.cs`**
- Added `IAmazonCloudFormation? _cloudFormationClient` nullable field for lazy caching.
- `GetCloudFormationClient()` creates the client on first call, returns cached instance on subsequent calls.
- Class now implements `IDisposable`; `Dispose()` disposes the cached client if created (null-safe).
- `IDisposable` was NOT added to `IAwsClientFactory` interface — factory interface stays minimal.
- Credential/region resolution logic is unchanged.

**`src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs`**
- Changed `var clientFactory = new AwsClientFactory(context);` → `using var clientFactory = new AwsClientFactory(context);` so factory (and its client) disposes at end of scope.
- Updated `ServiceFactory()` lambda to call `clientFactory.GetCloudFormationClient()`.

## Fix 2: Assert stack name mapping in aggregation test

**`src/MbUtils.AwsConsoleTui.Core.Tests/CloudFormation/CloudFormationServiceTests.cs`**
- In `ListStacks_MapsAndAggregatesAllPages`, added after existing count/status assertions:
  ```csharp
  Assert.Equal(new[] { "one", "three", "two" }, result.Select(s => s.Name).ToArray());
  ```
  The fake yields "one","two","three"; sorted case-insensitive ascending gives ["one","three","two"].
- No explicit `using System.Linq;` needed — already available via implicit global usings (the existing `ListStacks_SortsByNameCaseInsensitive` test uses `Select` with no explicit import).

## dotnet build summary

```
Build succeeded.
/src/.../AwsClientFactory.cs(42,16): warning CS0618: 'FallbackCredentialsFactory' is obsolete: ...
    1 Warning(s)
    0 Error(s)
```

**Note:** The CS0618 warning (`FallbackCredentialsFactory` obsolete) is pre-existing — it was present at line 30 of `AwsClientFactory.cs` before these changes. My changes moved the same call into a private method but did not introduce or remove it. Verified by stashing changes and confirming identical warning in original code.

## dotnet test result

```
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 21 ms
```

All 18 tests pass. Fix 2's new name assertion in `ListStacks_MapsAndAggregatesAllPages` is covered.

## Concerns

None. The pre-existing CS0618 warning for `FallbackCredentialsFactory` is a library deprecation notice that was present before this fix and is out of scope for this PR.
