# Factorio Mod Manager

A desktop application to manage Factorio mods: install, update, enable/disable, and organize mods into lists and groups.

This repository is a .NET 10 application with an Avalonia-based UI and a set of ViewModels and tests. It provides a focused workflow for maintaining Factorio mod collections, resolving dependencies, and applying batch updates.

## Key features (what users see)

- Install mods from the official Factorio mod portal or from a local zip (drag-and-drop or file picker).
- Update mods individually or run a one-click "Update All" that downloads and installs updates in parallel with a single aggregated progress indicator and a cancel option.
- Enable, disable, and manage which mods are active for your game; persistent mod lists let you switch between collections for different saves or playstyles.
- Create, rename, and organize mods into named lists and groups so you can maintain separate configurations (for example: "Vanilla+", "Multiplayer", "Experimental").
- Automatic dependency handling: missing dependencies are downloaded automatically when needed; dependencies are only enabled automatically when a parent mod requiring them is enabled.
- Compatibility detection: the app detects Factorio version and installed DLC and highlights mods that are incompatible or require a different game version.
- Backup and rollback: optional backups created before applying updates so you can restore previous mod files if an update breaks your setup.
- Search, filter, and sort mods by name, category, size, version, update availability, and enabled/disabled state to quickly find what you need.
- View rich mod details (description, author, available versions, changelog and file sizes) and choose specific versions to install when available.
- Import and export mod lists to share collections with friends or to move between machines.
- Notifications and automatic update checks (configurable) so you can be alerted when new versions are available.
- Store optional credentials/API tokens for authenticated access to the mod portal when needed.
- Conflict detection with clear warnings and manual resolution options when two mods are known to interfere with each other.

## User-facing settings and behavior

The application exposes a curated set of user-editable preferences (accessible via the Preferences/Settings UI). The list below reflects the settings implemented in the app's settings service and what they control for end users.

- `ModsPath` / `FactorioModsPath` (string)
  - Default: auto-detected via platform conventions if Factorio is present; otherwise empty
  - Example: `C:\Program Files\Steam\steamapps\common\Factorio\mods` or a custom folder like `D:\FactorioMods`
  - Use: where installed mod zip/folder files are located. Change this if you keep your mods in a non-standard location or on a different drive.

- `FactorioExePath` (string)
  - Default: empty (auto-detection attempted at startup)
  - Example: `C:\Program Files\Steam\steamapps\common\Factorio\bin\x64\factorio.exe`
  - Use: points the manager to the Factorio executable for actions that need version/DLC detection or launching the game.

- `ApiKey`, `Username`, `Token` (string, optional)
  - Use: credentials or API tokens stored for interacting with the Factorio mod portal or authenticated sources. These values are optional and may be left empty for anonymous access.

- `KeepOldModFiles` (boolean)
  - Default: false
  - Use: when enabled, old mod files may be preserved during update operations to allow manual rollback.

- `AutoCheckModUpdates` (boolean)
  - Default: true
  - Use: whether the app will automatically check for mod updates (used to drive in-app notifications and the updates pane).

- `CheckForAppUpdates` (boolean)
  - Default: true
  - Use: whether the manager will check for application updates.

- `UpdateConcurrency` (integer)
  - Default: 3
  - Minimum: 1
  - Use: controls the concurrency used by the "Update All" flow (how many mods can be updated in parallel). Raise this to speed up large batch updates if you have sufficient bandwidth.

- `VerboseDetectionLogging` (boolean)
  - Default: false
  - Use: when enabled, detection routines produce more detailed logs to assist with troubleshooting mod/version/DLC detection.

- `FactorioVersion` (string) and `HasSpaceAgeDlc` (boolean)
  - Typically auto-detected from the configured `FactorioExePath` / `FactorioDataPath`. These values are persisted so the app can filter or mark incompatible mods; advanced users can override them if needed.

- `FactorioDataPath` (string)
  - Use: explicit path to Factorio game data if automatic detection fails. Changing this triggers re-detection of installed DLC and version info.

- UI visibility and layout flags
  - `ShowGroupsPanel` (boolean) — whether the Groups panel is visible in the left column (default true)
  - `GroupsColumnWidth` (double) — last persisted width of the groups column (default 200.0)
  - `ShowCategoryColumn` / `ShowSizeColumn` (boolean) — whether the Category and Size columns are visible in the mods list (default true)

Notes on behavior and limitations
- The manager currently focuses on these settings; other advanced settings (download throttling, proxy configuration, automatic conflict resolution, or external backup locations) are not exposed via the settings service at this time.
- Dependency enablement policy: the app intentionally avoids enabling dependencies it installs unless a parent mod requiring them is enabled. This prevents surprises where installing a mod also changes gameplay by enabling additional mods.
- Update concurrency is the primary control for throttling parallel update work. There is not currently a separate per-download throttle or retry count exposed in user settings.

If you need additional settings (for example proxy support or explicit backup paths), open an issue or a PR — the codebase stores settings in a central `AppSettings` model and is straightforward to extend.

## Getting started (quick tasks)

1) Create a named mod list
  - Open the left-side Groups / Mod Lists panel.
  - Click the Add / Create list button in the Mod Lists header (the label or icon may vary by theme). The new list will enter rename mode so you can type a name (for example: `Multiplayer`).
  - Press Enter to save the name. The manager captures the currently enabled mods and persists the new list automatically via the Mod List service, so it is saved immediately and selected for further edits.

2) Install a mod
  - Option A (portal): Use the built-in mod browser/search to find a mod from the Factorio portal and click `Install` on the mod details.
  - Option B (local zip): Drag-and-drop a mod zip file onto the main window or use the File -> Install from ZIP menu. The manager will attempt dependency checks and download any missing dependencies if configured.

3) Run "Update All"
  - Open the Updates pane and click `Update All` to download and install updates for all installed mods.
  - The UI shows a single aggregated download progress indicator. You may cancel the batch update at any time; the app will attempt to stop ongoing downloads and avoid leaving partially-updated mods enabled.

These quick steps cover the most common workflows to start managing mod collections. For more advanced scenarios (import/export of mod lists, manual version selection, or troubleshooting detection) see the Development notes or open an issue for guidance.


## Troubleshooting

If you encounter problems, the following steps and settings often help diagnose and resolve common issues:

- Enable verbose detection logging: open Preferences and enable `VerboseDetectionLogging` to increase log detail for mod/version/DLC detection.
- Reproduce the issue after enabling verbose logging so the logs capture the failure path.
- Check the runtime logs and settings file (see Example Preferences) in the application's data folder for these keys: `FactorioModsPath`, `FactorioExePath`, `FactorioDataPath`, `FactorioVersion`, and `HasSpaceAgeDlc`. Confirm they point to the expected locations and versions.
- For update/download failures: verify network access to the Factorio portal, check `UpdateConcurrency` (lower it if downloads timeout), and try toggling `KeepOldModFiles` so you have backups available for manual rollback.
- If a mod is not detected or shows incorrect versioning, ensure the `FactorioExePath` / `FactorioDataPath` are correct and re-run detection (changing `FactorioDataPath` triggers re-detection).
- For authentication issues with the mod portal, verify `ApiKey`, `Username`, and `Token` values (clear and re-enter them if needed) and confirm the account has necessary permissions.
- When reporting issues, include: a brief description, steps to reproduce, the relevant log file(s) (with verbose logging enabled if applicable), and the `settings.json` fragment showing the keys above. Do not include secrets; redact tokens or API keys before uploading.


## Build and run

Requirements:
- .NET 10 SDK
- Visual Studio 2022+ / Visual Studio 2026 recommended (project in this workspace targets .NET 10)

From the command line:

1. Restore and build:

   `dotnet build`

2. Run tests:

   `dotnet test`

You can also open the solution in Visual Studio and run/debug the app from the IDE.

## Development notes

- UI is implemented with Avalonia (`*.axaml` views) and MVVM-style ViewModels.
- Follow existing code style and naming conventions when modifying files. Preserve private field names where present.
- The UpdatesHost implementation is expected to implement `IDisposable`, own a `CancellationTokenSource`, and provide a compact UI callback interface for progress/cancellation.

## Contributing

Contributions are welcome. Please follow the standard GitHub flow:

1. Fork the repository
2. Create a feature branch
3. Add tests for behavior you change
4. Open a pull request

Please run existing tests and ensure the build passes before submitting a PR.

## License

See `LICENSE` (if present) for licensing details.

