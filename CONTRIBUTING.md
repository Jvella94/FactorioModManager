# CONTRIBUTING

## Coding conventions

This project follows strict naming and formatting conventions. These are enforced in .editorconfig and by project code reviews.

### Private fields
- Private fields MUST use a leading underscore and camelCase: `_exampleField`, `_logService`, `_uiService`.
- Do NOT use snake_case (e.g. `_log_service`) or other styles for private fields.

### DTOs and immutability
- Small DTOs representing persisted JSON (like `ModListEntry`) should be immutable records when appropriate.
- Use container DTOs (e.g. `ModListDto`) for top-level JSON files to preserve shape and enable future fields without breaking compatibility.

### JSON persistence
- `mod-list.json` entries must include `name`, `enabled`, and an optional `version` field when an active version is set by the UI.
- Repository methods should read/write the `version` field and expose the combined state to services as DTOs.

### Services
- Keep service field naming as private leading underscore + camelCase for consistency across the codebase.
- When adding new service methods that change public API, update interfaces and DI registrations accordingly.

### Formatting and styling
- Follow the project's .editorconfig settings for indentation, newlines, var usage, and expression-bodied members.
- Prefer explicit error handling and logging using the provided `ILogService`.

## Project practices
- When modifying persistence behavior, add tests covering read/write round trips for the JSON files.
- When adding UI-visible changes (persisted version, timestamps), expose ViewModel properties to enable data-binding and unit testing.
