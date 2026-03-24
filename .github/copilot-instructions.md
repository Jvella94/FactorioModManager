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
- When manually installing mods from a zip file in the Factorio Mod Manager, use the API as the primary check for dependency checking, but fall back to the dependencies in info.json if the mod does not exist on the portal.

## Download Progress Management
- When multiple mods are being updated (batch flows), use `host.SetDownloadProgressVisible(true)` to show the aggregated download progress instead of calling `host.BeginSingleDownloadProgressAsync()`.

## Mod List Management
- When creating a mod list, automatically enter rename/edit mode, select the new list, and auto-focus the inline rename TextBox using a small focus-request flag on the item and code-behind to set focus.
- Show action buttons for groups/mod lists only when the item is selected.
- Provide collapsible sections for Groups and Mod Lists with independent expand state.
- Left-side Groups and Mod Lists should use compact item padding and spacing; expander content should stretch to fill available left-column height and provide internal scrolling (no fixed MaxHeight). Show action buttons only for selected items.

## Updates Management
- Prefer UpdatesHost refactor: use a compact IUpdateHostUi callback interface; UpdatesHost should implement IDisposable and own a CancellationTokenSource; replace multiple volatile bools with a single flag-backed state represented via properties accessing a volatile int flag (UpdateHostStateFlags).