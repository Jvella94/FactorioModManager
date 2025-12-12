````markdown name=.github/workflows/README.md url=https://github.com/Jvella94/FactorioModManager/blob/main/.github/workflows/README.md
# GitHub Actions Release Workflow

## Overview

This repository uses a single GitHub Actions workflow to build, package and publish the Factorio Mod Manager application for multiple platforms when a release tag is pushed. The workflow performs:

- Read version from the UI project CSProj and construct the release tag
- Build/publish the app per-platform (Linux, Windows, macOS Intel, macOS Apple Silicon)
- Package platform artifacts (tar.gz / zip)
- Create a GitHub Release and attach artifacts

This README documents the key behavior and the recent changes made to support native libraries (SkiaSharp) on macOS.

## Triggering releases

Create and push a tag starting with `v`:

```bash
# stable
git tag v1.0.0
git push origin v1.0.0

# prerelease
git tag v1.0.0-beta
git push origin v1.0.0-beta
```

When a tag is present this workflow will run (the workflow is set to run on changes to master in the repo — tagging behavior is handled by the set-version job).

## Build targets

The workflow builds/publishes for:

- linux-x64
- win-x64
- osx-x64 (Intel mac)
- osx-arm64 (Apple Silicon)

Each platform is produced as a self-contained publish.

## Important changes (why they were needed)

To fix runtime errors on macOS related to missing native SkiaSharp libraries (System.DllNotFoundException: libSkiaSharp), the workflow was updated:

- PublishSingleFile disabled for all platform publishes:
  -p:PublishSingleFile=false

  Single-file publish bundles native assets inside the host, which prevents dlopen from finding native .dylib files at runtime. Disabling single-file causes native runtime assets (like libSkiaSharp.dylib) to be emitted as separate files.

- Trimming disabled for publish:
  -p:PublishTrimmed=false

  Trimming can remove code used via reflection or native interop; disabling trimming avoids accidental removal of needed code.

- Packaging step enhanced for macOS:

  - The macOS packaging step now scans the entire publish output and moves native .dylib files (including libSkiaSharp\*.dylib) into the .app bundle's Contents/MacOS directory before the publish root is cleaned.
  - This ensures dyld can find the native libraries inside the .app bundle at runtime.

- The workflow continues to publish per-RID (osx-x64 and osx-arm64) so the correct native assets are produced per platform.

## Requirements

- .NET SDK 9.0 (workflow uses setup-dotnet@v4 dotnet-version: '9.0.x')
- If you build locally for macOS you should publish for the correct RID:
  - Intel: osx-x64
  - Apple Silicon: osx-arm64

## Project change required

Make sure the UI project includes the Skia native assets package for macOS so libSkiaSharp.dylib is included in the publish output. Add to UI/FactorioModManager.csproj:

```xml
<ItemGroup>
  <PackageReference Include="SkiaSharp.NativeAssets.macOS" Version="2.88.0" />
</ItemGroup>
```

(Replace `2.88.0` with the SkiaSharp.NativeAssets.macOS version that matches the SkiaSharp managed package(s) you use.)

## How the macOS packaging works

- dotnet publish outputs a directory like `publish/osx-x64/`
- The workflow creates an `.app` bundle skeleton and moves the built executable into:
  `publish/osx-x64/FactorioModManager.app/Contents/MacOS/FactorioModManager`
- Then the workflow searches the publish output (root and runtimes/\*) for:
  - libSkiaSharp\*.dylib
  - other `.dylib` files
- Those native libraries are moved into `Contents/MacOS/` so the runtime loader can locate them
- The rest of the publish directory is cleaned and the `.app` bundle is archived

This approach ensures native libraries shipped by NuGet native assets packages are present inside the .app bundle.

## Local test / publish commands

To reproduce locally for Intel mac:

```bash
# from repo root
dotnet restore

# publish for osx-x64 (do not single-file)
dotnet publish UI/FactorioModManager.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o ./publish/osx-x64
```

For Apple Silicon:

```bash
dotnet publish UI/FactorioModManager.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o ./publish/osx-arm64
```

After publishing locally, create an `.app` bundle (the workflow automates this) and verify native libs were produced and copied.

## Verifying the .app bundle

- Check for libSkiaSharp in the .app bundle:

```bash
ls -la publish/osx-x64/FactorioModManager.app/Contents/MacOS/libSkiaSharp*.dylib
```

- Run the executable with dyld diagnostics to see library loads:

```bash
DYLD_PRINT_LIBRARIES=1 ./publish/osx-x64/FactorioModManager.app/Contents/MacOS/FactorioModManager
```

If the `.dylib` exists in Contents/MacOS the loader should find it.

## Troubleshooting

- If you still get "Unable to load shared library 'libSkiaSharp'":

  - Confirm the correct SkiaSharp.NativeAssets.macOS package version is referenced in UI project.
  - Confirm the workflow or local publish produced libSkiaSharp.dylib under the publish tree before packaging.
  - Confirm the .app bundle contains libSkiaSharp.dylib in `Contents/MacOS/`.
  - Use DYLD_PRINT_LIBRARIES to see dlopen attempts (example above).

- Notarization / sandboxing: macOS notarization or App Store sandboxing may impose further constraints on binary layout and library signing. For distributing on macOS, ensure you follow Apple’s notarization/signing steps if required.

## Notes and gotchas

- The workflow still produces separate artifacts per-architecture. If you want a universal macOS binary you will need to build and merge binaries (this workflow does not create universal binaries).
- Single-file publish is convenient but incompatible with many native libraries that rely on dlopen; prefer non-single-file for apps that use native dependencies.
- If adding other native-dependency NuGet packages, ensure their native runtimes are published and moved into the .app by the packaging logic.

## What to change next (suggested improvements)

- Add explicit dependency checks in the CI to fail the build if expected native libs (e.g. libSkiaSharp.dylib) are not present in the packaged `.app`.
- Add macOS code signing / notarization steps if distributing outside of GitHub releases to increase compatibility on end-user machines.
- Consider publishing debug or release notes that explicitly call out macOS architecture built and whether native assets were included.

## Contact / Contributing

If you find the release artifact missing native libraries or the app fails to start on macOS, open an issue with:

- the platform (Intel or Apple Silicon)
- a link to the release artifact you downloaded
- output of `DYLD_PRINT_LIBRARIES=1` when running the executable (if available)

We’ll use that to diagnose packaging or publish issues.
````
