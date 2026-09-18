# Settings

The `/settings` page — home for user preferences, bring-your-own credentials, and inventory-behavior toggles (MEP-032).

## Sections

- **Display** — Imperial / Metric display system toggle (backed by `PreferencesService`).
- **AI (Claude API)** — paste, save, test, and remove the Anthropic API key; pick the Claude model (MEP-052) used by every Claude-backed feature. The AI-disabled banner disappears when a key is saved; substitution suggestions and dietary classification re-enable automatically. The model picker is visible and usable even with no key configured — it only persists a preference, it never calls Claude.
- **External Integrations** — stub section for future BYO-credential stories (Todoist in MEP-028 / MEP-029).
- **Inventory Behavior** — stub for MEP-027 auto-deplete-on-consume.

## Components

- `SettingsPageComponent` — route-hosted page with Material cards per section.
- `ConfirmDialogComponent` — small confirmation dialog used by the "Remove key" action.

## Services

- `SettingsService` (in `core/services/`) — HTTP calls for status / save / test / remove, and `saveModel` for the Claude model preference.
- `AiAvailabilityService` (in `core/services/`) — app-wide signal tracking whether the Claude key is configured and which model is selected; drives the persistent banner, in-page degraded notes, and the model picker's current value.
