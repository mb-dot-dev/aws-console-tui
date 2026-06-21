# Distribute `awstui` via Homebrew — Design

**Date:** 2026-06-21
**Status:** Approved for planning

## Overview

Distribute the AWS Console TUI as a single, standalone executable installable through Homebrew. On each version tag, GitHub Actions publishes a **self-contained, single-file `osx-arm64`** binary, attaches it to a **GitHub Release**, and updates a formula in a dedicated tap (`mb-dot-dev/homebrew-tap`) that downloads it. End users run:

```
brew install mb-dot-dev/tap/awstui
awstui
```

### Goals
- One command to install (`brew install mb-dot-dev/tap/awstui`) and one command to run (`awstui`).
- The binary is self-contained — runs on Apple Silicon macOS with **no .NET runtime installed**.
- Releases are fully automated: push a `vX.Y.Z` tag → Release built + formula updated.

### Non-goals (v1)
- No Intel (`osx-x64`) or Linux builds — Apple Silicon (`osx-arm64`) only.
- No trimming or Native AOT (the AWS SDK and Terminal.Gui rely on reflection; full self-contained avoids that risk).
- No Apple code-signing / notarization (Homebrew strips the quarantine attribute on formula-installed files, so the unsigned binary runs without Gatekeeper prompts).
- No submission to homebrew-core (a new niche tool does not meet its notability bar); a personal tap is the distribution channel.

### Why no signing is required
Homebrew removes the `com.apple.quarantine` extended attribute from files it installs from a formula, so a downloaded unsigned binary launches normally. Signing/notarization would only be needed if the raw binary were distributed outside Homebrew (e.g. a direct download); that is out of scope here.

## Component 1: Application changes (this repo)

### Assembly name
Set `<AssemblyName>awstui</AssemblyName>` in `src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj` so the published executable is named `awstui` (today it is `MbUtils.AwsConsoleTui.ConsoleApp`).

### Publish configuration
Self-contained single-file publish, driven from the command line in CI (no machine-specific defaults baked into the csproj beyond what is portable):
- Runtime identifier: `osx-arm64`
- `--self-contained true`
- `-p:PublishSingleFile=true`
- No `PublishTrimmed`, no `PublishAot`.

The binary has no native sidecar dependencies to manage: Terminal.Gui and the AWS SDK are managed assemblies, so single-file self-extraction works.

### Non-interactive CLI flags
Add handling for `--version`/`-v` and `--help`/`-h` that runs **before** `Application.Init()` and exits without starting the TUI. This is required because the Homebrew formula's `test do` block must run a non-interactive command (launching the full-screen TUI would hang CI), and it is standard CLI behavior.

Design for testability:
- A pure parser in `Core` (e.g. `MbUtils.AwsConsoleTui.Core.Cli.StartupArgs.Parse(string[] args) -> StartupAction`) where `StartupAction` is one of: `RunTui`, `ShowVersion`, `ShowHelp`. Pure and unit-tested (TDD).
- `Program.cs` calls the parser first. For `ShowVersion` it writes the version string and returns 0; for `ShowHelp` it writes a short usage block and returns 0; for `RunTui` it proceeds to the existing `Application.Create().Init()` flow.
- The version string is sourced from the assembly's informational version (set at build time from the tag — see Versioning), so `awstui --version` reports the released version.

## Component 2: Release automation (GitHub Actions)

A workflow at `.github/workflows/release.yml` triggered on tags matching `v*`:

1. **Runner:** `macos-14` (Apple Silicon). Set up .NET 10 SDK.
2. **Gate:** `dotnet test` — fail the release if tests fail.
3. **Derive version:** strip the leading `v` from the tag (`v0.1.0` → `0.1.0`).
4. **Publish:** `dotnet publish src/MbUtils.AwsConsoleTui.ConsoleApp -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:Version=<version>`.
5. **Package:** tar+gzip the `awstui` binary as `awstui-<version>-osx-arm64.tar.gz` (a tarball preserves the executable bit). Compute its SHA256.
6. **Release:** create the GitHub Release for the tag and upload the tarball as an asset.
7. **Bump formula:** update `version`, `url`, and `sha256` in `mb-dot-dev/homebrew-tap`'s `Formula/awstui.rb` and push the change, authenticated with a PAT stored as the repo secret `HOMEBREW_TAP_TOKEN` (e.g. via a `mislav/bump-homebrew-formula`-style action or an equivalent scripted commit).

## Component 3: The tap and formula

A separate public repository **`mb-dot-dev/homebrew-tap`** (Homebrew maps the tap name `mb-dot-dev/tap` to the repo `homebrew-tap`). It contains `Formula/awstui.rb`:

```ruby
class Awstui < Formula
  desc "Terminal UI for the AWS Console"
  homepage "https://github.com/mb-dot-dev/aws-console-tui"
  version "0.1.0"
  url "https://github.com/mb-dot-dev/aws-console-tui/releases/download/v0.1.0/awstui-0.1.0-osx-arm64.tar.gz"
  sha256 "<computed at release>"
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

The release workflow keeps `version`, `url`, and `sha256` current; the rest of the formula is stable.

## Prerequisites (one-time, manual by the maintainer)

These are performed by the user, not by the implementation:
- Create the public repo **`mb-dot-dev/homebrew-tap`** with an initial `Formula/awstui.rb` (the plan provides the file contents; the first release fills in the real `sha256`).
- Create a Personal Access Token with **contents: write on the tap repo** and add it to the app repo as the Actions secret **`HOMEBREW_TAP_TOKEN`**.

## Versioning

- The git tag is the source of truth: `vMAJOR.MINOR.PATCH`.
- CI strips the leading `v` and passes the version into the build (`-p:Version=`), which flows into the assembly informational version reported by `awstui --version`.
- The formula's `version`/`url`/`sha256` are derived from the same tag and release asset.

## Testing & verification

- **Unit-tested (TDD):** the `StartupArgs.Parse` CLI parser — `--version`/`-v` → `ShowVersion`, `--help`/`-h` → `ShowHelp`, no/other args → `RunTui`, and that an explicit run is the default.
- **Not unit-testable:** the GitHub Actions workflow and the Ruby formula. These are validated once by an end-to-end dry run: push a `v0.1.0` (or a prerelease) tag, confirm CI creates the Release and updates the formula, then on a clean machine run `brew install mb-dot-dev/tap/awstui` and `awstui --version` (expect the released version) and `awstui` (expect the TUI to launch).
- The existing Core test suite continues to pass and remains the build gate in CI.

## File / change summary

In this repo:
- `src/MbUtils.AwsConsoleTui.ConsoleApp/MbUtils.AwsConsoleTui.ConsoleApp.csproj` — add `AssemblyName`.
- `src/MbUtils.AwsConsoleTui.Core/Cli/StartupArgs.cs` (new) — pure arg parser + `StartupAction`.
- `src/MbUtils.AwsConsoleTui.Core.Tests/Cli/StartupArgsTests.cs` (new) — parser tests.
- `src/MbUtils.AwsConsoleTui.ConsoleApp/Program.cs` — call the parser, handle version/help, then run the TUI.
- `.github/workflows/release.yml` (new) — the release workflow.

In the separate tap repo (`mb-dot-dev/homebrew-tap`, created by the maintainer):
- `Formula/awstui.rb` — the formula (contents provided by the plan).
