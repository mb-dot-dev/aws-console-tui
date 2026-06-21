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
formula. The formula change is committed directly to the tap (no pull request to merge) — this is why the `HOMEBREW_TAP_TOKEN` needs Contents: write on the tap repo.

## Installing (end users)

```bash
brew install mb-dot-dev/tap/awstui
awstui
```
