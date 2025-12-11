# GitHub Actions Release Workflow

## Overview

This repository uses GitHub Actions to automatically build and release the Factorio Mod Manager application for multiple platforms.

## How It Works

### Triggering Releases

The workflow is triggered when you push a git tag starting with `v`:

```bash
# For stable releases
git tag v1.0.0
git push origin v1.0.0

# For bleeding edge releases
git tag v1.0.0-beta
git push origin v1.0.0-beta
```

### Release Types

The workflow automatically determines the release type based on the tag name:

- **Stable Release**: Tags without pre-release indicators (e.g., `v1.0.0`, `v2.1.3`)
- **Bleeding Edge (Pre-release)**: Tags containing keywords like `alpha`, `beta`, `rc`, `dev`, `preview`, or `pre` (e.g., `v1.0.0-beta`, `v2.0.0-rc1`, `v1.5.0-dev`)

### Build Platforms

The workflow builds the application for three platforms:
- **Linux** (linux-x64)
- **Windows** (win-x64)
- **macOS** (osx-x64)

### Artifacts

For each platform, the workflow creates:
- **Linux/macOS**: `.tar.gz` archives
- **Windows**: `.zip` archives

All artifacts are automatically attached to the GitHub release.

### Workflow Steps

1. **Build**: Compiles the application in Release configuration for each platform
2. **Publish**: Creates self-contained executables for each platform
3. **Archive**: Packages the builds into platform-appropriate archives
4. **Release**: Creates a GitHub release with all artifacts attached

## Examples

### Creating a Stable Release

```bash
git tag v1.0.0
git push origin v1.0.0
```

This creates a stable release marked as the latest release on GitHub.

### Creating a Bleeding Edge Release

```bash
git tag v1.1.0-beta
git push origin v1.1.0-beta
```

This creates a pre-release that is clearly marked as bleeding edge/unstable.

## Requirements

- .NET 9.0 SDK (automatically installed by the workflow)
- Valid git tags following semantic versioning

## Notes

- Release notes are automatically generated from commit messages
- The workflow requires `contents: write` permission to create releases
- All builds are self-contained and include the .NET runtime
