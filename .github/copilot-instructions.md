# Copilot Instructions

## General Guidelines
- First general instruction
- Second general instruction

## Code Style
- Use specific formatting rules
- Follow naming conventions
- When modifying code, do not rename private fields; preserve existing private field names.

## Dependency Management
- When installing dependencies for mod updates, only enable a dependency automatically if at least one parent mod requiring it is enabled; otherwise, keep it disabled after installation.

## Download Progress Management
- When multiple mods are being updated (batch flows), use `host.SetDownloadProgressVisible(true)` to show the aggregated download progress instead of calling `host.BeginSingleDownloadProgressAsync()`.