# AWS Console TUI — CloudFormation Stacks (v1) — Design

**Date:** 2026-06-21
**Status:** Approved for planning

## Overview

A terminal UI for the AWS Console built on [Terminal.Gui](https://gui-cs.github.io/Terminal.Gui/) v2 (`Terminal.Gui` 2.4.7, .NET 10). The first feature is a **read-only CloudFormation stacks list**. The app is structured so future services (CloudFormation stack details, S3 buckets, Lambda functions, SQS queues) slot in without restructuring.

### Goals (v1)
- Authenticate via the AWS SDK default credential chain, with an in-app profile **and** region picker.
- Display a read-only, filterable, refreshable list of CloudFormation stacks.
- Establish a clean, testable architecture (Core library + UI + tests) that scales to future services.
- Surface the roadmap of future services as disabled menu placeholders.

### Non-goals (v1)
- No mutating AWS actions (no create/update/delete stack).
- No stack details view yet (the row-selection seam is wired but inert).
- No S3 / Lambda / SQS functionality yet (menu placeholders only).
- No unit testing of the Terminal.Gui UI layer.

## Solution structure

Three projects:

- **`MbUtils.AwsConsoleTui.Core`** — new class library (net10.0). AWS abstractions, plain domain models, and pure logic (filtering, mapping). **No Terminal.Gui dependency.** References the AWS SDK.
- **`MbUtils.AwsConsoleTui.ConsoleApp`** — existing console app. Terminal.Gui UI only. References Core.
- **`MbUtils.AwsConsoleTui.Core.Tests`** — new xUnit test project. Unit tests for Core logic.

All AWS access lives behind interfaces in Core so the UI and tests never touch the live SDK directly.

NuGet packages:
- Core: `AWSSDK.CloudFormation`, `AWSSDK.SecurityToken` (as needed for credential resolution).
- Core.Tests: `xunit`, `xunit.runner.visualstudio`, a mocking approach (hand-written fakes preferred for the client adapter).

## AWS access layer (Core)

### Domain model
```
StackInfo {
    string Name
    string Status
    DateTime CreatedAt
    DateTime? LastUpdatedAt
    string? Description
}
```
Our own type, decoupled from the SDK's `Stack`, so the UI and tests don't depend on SDK types.

### Interfaces
- **`IAwsContext`** — holds the selected profile name and region; the single source of truth for "who/where". Mutable so `Switch Profile/Region…` can update it.
- **`IAwsClientFactory`** — builds SDK clients (e.g. `AmazonCloudFormationClient`) from the current `IAwsContext`.
- **`IProfileProvider`** — enumerates named profiles from the shared AWS config via `CredentialProfileStoreChain`; exposes the available region list from the SDK's region endpoints. Provides env-based defaults (`AWS_PROFILE` / `AWS_REGION`) when present.
- **`ICloudFormationService`**
  - `Task<IReadOnlyList<StackInfo>> ListStacksAsync(CancellationToken ct)` — uses the `DescribeStacks` paginator and maps each SDK `Stack` → `StackInfo`. `DescribeStacks` (no stack name) returns all non-deleted stacks with exactly the columns we need.

### Pure logic
- **`StackFilter`** — case-insensitive substring filter on stack name. An empty/whitespace filter returns the full list. Pure and unit-tested.
- **SDK → `StackInfo` mapping** — isolated and unit-tested, including null `LastUpdatedTime` and null `Description`.

### Client adapter for testability
The SDK client is wrapped behind a thin adapter interface (e.g. `ICloudFormationClient` exposing the paginated `DescribeStacks` call) so `CloudFormationService`'s mapping/aggregation is testable with a hand-written fake that returns canned pages.

## UI shell (ConsoleApp)

### Startup flow
1. `Application.Init`.
2. Modal **Profile/Region dialog** — two pickers (named profiles, regions) with defaults from env vars. On confirm, populate `IAwsContext`.
3. Build and run the main top-level (MenuBar + content + StatusBar).
4. `Application.Shutdown` on exit.

### MenuBar (top)
- **`File`** ▸ `Quit`
- **`CloudFormation`** ▸ `Stacks` *(active)* · `Stack Details` *(placeholder, disabled — to be opened via row selection later)*
- **`S3`** ▸ `Buckets` *(placeholder, disabled)*
- **`Lambda`** ▸ `Functions` *(placeholder, disabled)*
- **`SQS`** ▸ `Queues` *(placeholder, disabled)*
- **`Session`** ▸ `Switch Profile/Region…`

Placeholders render as greyed-out, disabled menu items so the roadmap is visible but unclickable. As each service lands, its item is enabled and wired to a `Window`; no menu restructuring needed.

### StatusBar (bottom)
Shows `profile | region | status (Ready / Loading… / error)` plus key hints (`F5 Refresh`, `Ctrl-Q Quit`).

### Service views
Each service opens as its own `Window` (the "tabbed/windowed" desktop-app feel). v1 ships one: the Stacks view.

## The Stacks view

A `Window` containing:
- A **filter `TextField`** at the top.
- A **`TableView`** listing stacks with columns: **Name, Status, Created, Last Updated, Description**, sorted by name.
- A per-view status line.

Behavior:
- **`F5`** refreshes (re-fetches from AWS).
- Typing in the filter re-applies `StackFilter` live against the already-loaded list — **no extra API call**.
- Selecting / pressing **Enter** on a row is **wired but no-ops** — the seam for the future Stack Details view.

## Data flow & async

```
Profile/Region dialog
  → IAwsContext (profile, region)
  → IAwsClientFactory (builds CFN client)
  → ICloudFormationService.ListStacksAsync
  → IReadOnlyList<StackInfo>
  → StackFilter
  → TableView
```

AWS calls run off the UI thread; results are marshalled back onto the UI thread with `Application.Invoke`. The UI shows a `Loading…` state during the fetch and remains responsive throughout.

## Error handling

Credential/expiry, authorization, and network errors are caught at the service boundary and surfaced via `MessageBox.ErrorQuery` plus a status-bar message. The app stays usable after an error — the user can switch profile/region or retry the refresh without restarting.

## Testing (TDD)

Unit tests target Core only:
- `StackFilter` — substring match, case-insensitivity, empty filter returns all, no-match returns empty.
- SDK `Stack` → `StackInfo` mapping — including null `LastUpdatedTime` and null `Description`.
- `CloudFormationService.ListStacksAsync` — aggregation across paginated `DescribeStacks` results, via a fake `ICloudFormationClient`.

The Terminal.Gui UI layer is not unit-tested.

## Future work (out of scope for v1)

- CloudFormation stack details (opened via row selection; enable the disabled menu item).
- S3 buckets list.
- Lambda functions list.
- SQS queues list.

Each future service follows the same pattern: a Core service interface + model, a UI `Window`, and an enabled menu item.
