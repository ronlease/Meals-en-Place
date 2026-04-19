# Settings

The `/settings` page — home for user preferences, bring-your-own credentials, and inventory-behavior toggles (MEP-032).

## Sections

- **Display** — Imperial / Metric display system toggle (backed by `PreferencesService`).
- **AI (Claude API)** — paste, save, test, and remove the Anthropic API key. The AI-disabled banner disappears when a key is saved; substitution suggestions and dietary classification re-enable automatically.
- **External Integrations** — stub section for future BYO-credential stories (Todoist in MEP-028 / MEP-029).
- **Inventory Behavior** — stub for MEP-027 auto-deplete-on-consume.

## Components

- `SettingsPageComponent` — route-hosted page with Material cards per section.
- `ConfirmDialogComponent` — small confirmation dialog used by the "Remove key" action.

## Services

- `SettingsService` (in `core/services/`) — HTTP calls for status / save / test / remove.
- `AiAvailabilityService` (in `core/services/`) — app-wide signal tracking whether the Claude key is configured; drives the persistent banner and in-page degraded notes.
