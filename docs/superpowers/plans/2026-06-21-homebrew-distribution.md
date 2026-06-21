# Homebrew Distribution of `awstui` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the AWS Console TUI as a self-contained single-file `osx-arm64` binary installable via `brew install mb-dot-dev/tap/awstui`, with a tag-triggered GitHub Actions release that builds the binary and updates a formula in a dedicated tap.

**Architecture:** Add non-interactive `--version`/`--help` flags (a pure parser in Core + wiring in `Program.cs`) so the binary has a testable, TTY-free entrypoint and the formula can self-test. Rename the published assembly to `awstui`. A `release.yml` workflow publishes the single-file binary on each `v*` tag, creates a GitHub Release, and bumps the formula in `mb-dot-dev/homebrew-tap`. The formula and one-time setup are captured as a versioned template + README in this repo.

**Tech Stack:** .NET 10, C#, xUnit, GitHub Actions (macos-14 runner), Homebrew (Ruby formula).

## Global Constraints

- Target framework: `net10.0`.
- Platform: **`osx-arm64` only** (Apple Silicon). No `osx-x64`, no Linux.
- Build mode: **self-contained single-file** (`--self-contained true -p:PublishSingleFile=true`). **No** `PublishTrimmed`, **no** `PublishAot`.
- Installed command / formula name: **`awstui`** (published `AssemblyName` is `awstui`).
- Tap: **`mb-dot-dev/homebrew-tap`** (Homebrew tap name `mb-dot-dev/tap`).
- Release asset name: **`awstui-<version>-osx-arm64.tar.gz`** (a tar.gz, to preserve the executable bit).
- Version is sourced from the git tag: `vMAJOR.MINOR.PATCH` → `MAJOR.MINOR.PATCH`, passed via `-p:Version=`.
- The formula `test do` runs `#{bin}/awstui --version` and asserts the version — so `--version` must print the version and exit without launching the TUI.
- No Apple code-signing / notarization (Homebrew strips the quarantine attribute).
- Cross-repo formula push uses the PAT secret **`HOMEBREW_TAP_TOKEN`** (manual prerequisite).

---

### Task 1: CLI startup-arg parser (Core)

**Files:**
- Create: `src/MbUtils.AwsConsoleTui.Core/Cli/StartupArgs.cs`
- Test: `src/MbUtils.AwsConsoleTui.Core.Tests/Cli/StartupArgsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum MbUtils.AwsConsoleTui.Core.Cli.StartupAction { RunTui, ShowVersion, ShowHelp }`
  - `static StartupAction StartupArgs.Parse(IReadOnlyList<string> args)` — returns `ShowVersion` for `--version`/`-v`, `ShowHelp` for `--help`/`-h`, otherwise `RunTui`. First recognized flag in argument order wins; unrecognized args are ignored (→ `RunTui`).

- [ ] **Step 1: Write the failing tests**

`src/MbUtils.AwsConsoleTui.Core.Tests/Cli/StartupArgsTests.cs`:

```csharp
using MbUtils.AwsConsoleTui.Core.Cli;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.Cli;

public class StartupArgsTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void VersionFlag_ReturnsShowVersion(string arg)
        => Assert.Equal(StartupAction.ShowVersion, StartupArgs.Parse(new[] { arg }));

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpFlag_ReturnsShowHelp(string arg)
        => Assert.Equal(StartupAction.ShowHelp, StartupArgs.Parse(new[] { arg }));

    [Fact]
    public void NoArgs_ReturnsRunTui()
        => Assert.Equal(StartupAction.RunTui, StartupArgs.Parse(Array.Empty<string>()));

    [Fact]
    public void UnknownArg_ReturnsRunTui()
        => Assert.Equal(StartupAction.RunTui, StartupArgs.Parse(new[] { "--frobnicate" }));

    [Fact]
    public void FirstRecognizedFlagInOrderWins()
        => Assert.Equal(StartupAction.ShowHelp, StartupArgs.Parse(new[] { "--help", "--version" }));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~StartupArgsTests"`
Expected: FAIL — `StartupArgs` / `StartupAction` do not exist (compile error).

- [ ] **Step 3: Implement the parser**

`src/MbUtils.AwsConsoleTui.Core/Cli/StartupArgs.cs`:

```csharp
namespace MbUtils.AwsConsoleTui.Core.Cli;

public enum StartupAction
{
    RunTui,
    ShowVersion,
    ShowHelp,
}

public static class StartupArgs
{
    public static StartupAction Parse(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--version":
                case "-v":
                    return StartupAction.ShowVersion;
                case "--help":
                case "-h":
                    return StartupAction.ShowHelp;
            }
        }

        return StartupAction.RunTui;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~StartupArgsTests"`
Expected: PASS — 7 passed (2 + 2 theory cases + 3 facts).

- [ ] **Step 5: Commit**

```bash
git add src/MbUtils.AwsConsoleTui.Core/Cli/StartupArgs.cs src/MbUtils.AwsConsoleTui.Core.Tests/Cli/StartupArgsTests.cs
git commit -m "feat: add startup CLI arg parser (--version/--help)"
```

---

### Task 2: Wire CLI flags into Program.cs and rename the assembly

**Files:**
- Modify: `src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj`
- Modify: `src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs` (full replace)

**Interfaces:**
- Consumes: `StartupArgs.Parse(IReadOnlyList<string>)` and `StartupAction` (Task 1); the existing UI types `ProfileRegionDialog`, `AppMenu`, `AppStatusBar`, `StacksView`, and Core `ProfileProvider`, `AwsContext`, `AwsClientFactory`, `CloudFormationService`, `CloudFormationClient`.
- Produces: an `awstui` executable that prints version/help and exits for those flags, otherwise launches the TUI unchanged.

- [ ] **Step 1: Set the assembly name**

In `src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj`, add `<AssemblyName>awstui</AssemblyName>` to the existing `<PropertyGroup>` so it reads:

```xml
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>awstui</AssemblyName>
  </PropertyGroup>
```

- [ ] **Step 2: Replace Program.cs to dispatch on the parsed action**

`src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs` (full file):

```csharp
using System.Reflection;
using MbUtils.AwsConsoleTui.ConsoleApp.Ui;
using MbUtils.AwsConsoleTui.Core.Aws;
using MbUtils.AwsConsoleTui.Core.Cli;
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// Handle non-interactive flags before touching Terminal.Gui so they work
// without a TTY (the Homebrew formula self-test runs `awstui --version`).
switch (StartupArgs.Parse(args))
{
    case StartupAction.ShowVersion:
        Console.WriteLine(GetVersion());
        return;
    case StartupAction.ShowHelp:
        Console.WriteLine(HelpText());
        return;
}

// v2 instance-based application lifecycle: Create + Init returns an IApplication
// that owns its resources and is disposed by the using block (replaces the
// legacy static Application.Init/Run/Shutdown).
using IApplication app = Application.Create().Init();

var profileProvider = new ProfileProvider();
var context = new AwsContext();
using var clientFactory = new AwsClientFactory(context);

ICloudFormationService ServiceFactory() =>
    new CloudFormationService(new CloudFormationClient(clientFactory.GetCloudFormationClient()));

var stacksView = new StacksView(app, ServiceFactory)
{
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
};

var menu = AppMenu.Build(app, onShowStacks: stacksView.Reload);

var content = new Window
{
    Title = "CloudFormation Stacks",
    X = 0,
    Y = Pos.Bottom(menu),
    Width = Dim.Fill(),
    Height = Dim.Fill(1),
};
content.Add(stacksView);

// Window serves as the top-level IRunnable (Toplevel was removed in v2).
var top = new Window();
top.Add(menu, content);

// Run the main window once. Once its loop is live, show the profile/region
// picker as a modal on top of it (the v2 session-stack pattern). Deferring with
// app.Invoke ensures the main loop is iterating before the nested modal runs.
top.Initialized += (_, _) => app.Invoke(() =>
{
    var (profile, region) = ProfileRegionDialog.Show(app, profileProvider);
    if (profile is null || region is null)
    {
        app.RequestStop(top);
        return;
    }

    context.Set(profile, region);
    top.Add(AppStatusBar.Build(app, context));
    top.SetNeedsDraw();
    stacksView.Reload();
});

app.Run(top);
top.Dispose();

static string GetVersion()
{
    var info = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    // Strip build metadata (e.g. "0.1.0+abc123" -> "0.1.0").
    var plus = info.IndexOf('+');
    return plus >= 0 ? info[..plus] : info;
}

static string HelpText() =>
    """
    awstui — Terminal UI for the AWS Console

    Usage:
      awstui            Launch the interactive TUI
      awstui --version  Print the version and exit
      awstui --help     Show this help and exit
    """;
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: build succeeds, 0 errors, 0 warnings.

- [ ] **Step 4: Run the full test suite (regression)**

Run: `dotnet test`
Expected: PASS — all Core tests (23: 18 existing + 5 new StartupArgs cases) pass.

- [ ] **Step 5: Verify the flags non-interactively**

Run: `dotnet run --project src/MbUtils.AwsConsoleTui.ConsoleApp -- --version`
Expected: prints a version line (e.g. `1.0.0` locally, since no `-p:Version` is passed) and exits 0 — the TUI does NOT launch.

Run: `dotnet run --project src/MbUtils.AwsConsoleTui.ConsoleApp -- --help`
Expected: prints the usage block and exits 0.

Run: `dotnet run --project src/MbUtils.AwsConsoleTui.ConsoleApp -- -v`
Expected: same as `--version`.

(Do NOT run `dotnet run` with no args here — that launches the full TUI and needs a terminal.)

- [ ] **Step 6: Verify the published binary is named `awstui`**

Run: `dotnet publish src/MbUtils.AwsConsoleTui.ConsoleApp -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:Version=0.0.1-test -o /tmp/awstui-pub && /tmp/awstui-pub/awstui --version`
Expected: the file `/tmp/awstui-pub/awstui` exists and prints `0.0.1-test`.

- [ ] **Step 7: Commit**

```bash
git add src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs
git commit -m "feat: rename assembly to awstui and add --version/--help flags"
```

---

### Task 3: Release workflow (GitHub Actions)

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: the `awstui` published binary (Task 2); the repo secret `HOMEBREW_TAP_TOKEN` (manual prerequisite, Task 4).
- Produces: on a `v*` tag — a GitHub Release with `awstui-<version>-osx-arm64.tar.gz`, and a formula bump committed to `mb-dot-dev/homebrew-tap`.

- [ ] **Step 1: Create the workflow**

`.github/workflows/release.yml`:

```yaml
name: release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write # create the GitHub Release in this repo

jobs:
  release:
    runs-on: macos-14 # Apple Silicon runner -> native osx-arm64 build & test
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Derive version from tag
        id: version
        run: echo "version=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"

      - name: Test
        run: dotnet test --configuration Release

      - name: Publish self-contained single-file (osx-arm64)
        run: |
          dotnet publish src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj \
            -c Release -r osx-arm64 --self-contained true \
            -p:PublishSingleFile=true -p:Version=${{ steps.version.outputs.version }} \
            -o publish

      - name: Package tarball
        id: package
        run: |
          TARBALL="awstui-${{ steps.version.outputs.version }}-osx-arm64.tar.gz"
          tar -C publish -czf "$TARBALL" awstui
          echo "tarball=$TARBALL" >> "$GITHUB_OUTPUT"

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: ${{ steps.package.outputs.tarball }}
          fail_on_unmatched_files: true

      - name: Bump Homebrew formula in tap
        uses: mislav/bump-homebrew-formula-action@v3
        with:
          formula-name: awstui
          homebrew-tap: mb-dot-dev/homebrew-tap
          download-url: https://github.com/mb-dot-dev/aws-console-tui/releases/download/${{ github.ref_name }}/${{ steps.package.outputs.tarball }}
        env:
          COMMITTER_TOKEN: ${{ secrets.HOMEBREW_TAP_TOKEN }}
```

- [ ] **Step 2: Validate the YAML parses**

Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml')); print('YAML OK')"`
Expected: prints `YAML OK` (no parse error).

- [ ] **Step 3: Sanity-check the publish/package commands locally**

Run: `dotnet publish src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:Version=0.0.1-test -o /tmp/rel-pub && tar -C /tmp/rel-pub -czf /tmp/awstui-test.tar.gz awstui && tar -tzf /tmp/awstui-test.tar.gz`
Expected: the tar listing shows `awstui` (confirms the publish output dir + tar command used in the workflow are correct).

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: add tag-triggered release workflow for osx-arm64"
```

---

### Task 4: Homebrew formula template and setup docs

**Files:**
- Create: `packaging/homebrew/awstui.rb`
- Create: `packaging/homebrew/README.md`

**Interfaces:**
- Consumes: nothing in code; this is the canonical formula source that the maintainer copies into `mb-dot-dev/homebrew-tap` and that the release workflow (Task 3) keeps updated.
- Produces: a version-controlled formula template and one-time setup instructions.

- [ ] **Step 1: Create the formula template**

`packaging/homebrew/awstui.rb`:

```ruby
class Awstui < Formula
  desc "Terminal UI for the AWS Console"
  homepage "https://github.com/mb-dot-dev/aws-console-tui"
  version "0.0.0"
  url "https://github.com/mb-dot-dev/aws-console-tui/releases/download/v0.0.0/awstui-0.0.0-osx-arm64.tar.gz"
  sha256 "0000000000000000000000000000000000000000000000000000000000000000"
  depends_on macos: :big_sur
  depends_on arch: :arm64

  def install
    bin.install "awstui"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/awstui --version")
  end
end
```

- [ ] **Step 2: Create the setup README**

`packaging/homebrew/README.md`:

```markdown
# Homebrew distribution

`awstui` is distributed through a dedicated tap. The formula here is the
canonical source; the release workflow keeps `version`, `url`, and `sha256`
up to date on each `v*` tag.

## One-time setup (maintainer)

1. Create a public repo `mb-dot-dev/homebrew-tap`.
2. Copy `packaging/homebrew/awstui.rb` into it as `Formula/awstui.rb` and
   commit. (The placeholder `version`/`url`/`sha256` are replaced by the first
   release.)
3. Create a Personal Access Token scoped to **only** `mb-dot-dev/homebrew-tap`
   with **Contents: Read and write**. Add it to the `aws-console-tui` repo as
   the Actions secret `HOMEBREW_TAP_TOKEN`.

## Cutting a release

```bash
git tag v0.1.0
git push origin v0.1.0
```

The `release` workflow builds the `osx-arm64` single-file binary, creates a
GitHub Release with `awstui-0.1.0-osx-arm64.tar.gz`, and updates the tap
formula.

## Installing (end users)

```bash
brew install mb-dot-dev/tap/awstui
awstui
```
```

- [ ] **Step 3: Confirm the formula matches the workflow's asset naming**

Run: `grep -q 'awstui-0.0.0-osx-arm64.tar.gz' packaging/homebrew/awstui.rb && grep -q 'bin.install "awstui"' packaging/homebrew/awstui.rb && echo "FORMULA OK"`
Expected: prints `FORMULA OK` (the URL asset pattern and installed binary name are consistent with Task 3's `awstui-<version>-osx-arm64.tar.gz` and the `awstui` binary).

- [ ] **Step 4: Commit**

```bash
git add packaging/homebrew/awstui.rb packaging/homebrew/README.md
git commit -m "docs: add Homebrew formula template and tap setup guide"
```

---

## Manual steps (outside this plan — the maintainer performs once)

These cannot be automated by the implementation and are required before the first real release works end to end:
1. Create the public repo `mb-dot-dev/homebrew-tap` with `Formula/awstui.rb` (copied from `packaging/homebrew/awstui.rb`).
2. Create the `HOMEBREW_TAP_TOKEN` PAT secret (contents:write on the tap repo) in the `aws-console-tui` repo.
3. Cut a tag (`git tag v0.1.0 && git push origin v0.1.0`) and verify: the Release is created, the formula is bumped, and on a clean machine `brew install mb-dot-dev/tap/awstui` then `awstui --version` reports `0.1.0` and `awstui` launches the TUI.

## Self-Review Notes

- **Spec coverage:**
  - AssemblyName `awstui` → Task 2 Step 1. ✓
  - Self-contained single-file `osx-arm64`, no trim/AOT → Task 2 Step 6, Task 3 publish step, Global Constraints. ✓
  - `--version`/`-v`, `--help`/`-h` before `Application.Init` → Task 1 (parser) + Task 2 (wiring). ✓
  - Pure, unit-tested parser in Core (`StartupArgs.Parse` → `StartupAction`) → Task 1. ✓
  - Version from assembly informational version, build-metadata stripped → Task 2 `GetVersion()`. ✓
  - Tag-triggered workflow on `v*`, macos-14, dotnet test gate, publish, tar.gz, SHA256, Release, formula bump → Task 3. ✓
  - Version derived from tag via `-p:Version=` → Task 3 derive-version + publish steps. ✓
  - Asset name `awstui-<version>-osx-arm64.tar.gz` → Task 3 package step, Task 4 formula url. ✓
  - Dedicated tap `mb-dot-dev/homebrew-tap`, formula with `--version` test → Task 4. ✓
  - `HOMEBREW_TAP_TOKEN` cross-repo push → Task 3 bump step + Task 4 README. ✓
  - One-time prerequisites (tap repo, PAT) → Task 4 README + Manual steps section. ✓
  - No signing/notarization → documented in spec; nothing to implement. ✓
- **Type consistency:** `StartupArgs.Parse(IReadOnlyList<string>)` and `StartupAction { RunTui, ShowVersion, ShowHelp }` are defined in Task 1 and consumed with identical names in Task 2. The binary name `awstui`, asset name `awstui-<version>-osx-arm64.tar.gz`, and tap `mb-dot-dev/homebrew-tap` are used identically across Tasks 2–4.
- **Notes for the executor:** Tasks 3 and 4 are infrastructure/docs — they are validated by YAML parse, local publish/tar dry-runs, and consistency greps, not by unit tests. The true end-to-end validation (a real tagged release + `brew install`) is a maintainer manual step and intentionally outside the automated plan.
```
