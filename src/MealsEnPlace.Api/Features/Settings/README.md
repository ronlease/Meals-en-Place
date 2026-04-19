# Settings

Bring-your-own Anthropic API key flow and the availability gate every Claude-backed service consults (MEP-032).

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/settings/claude/status` | Returns `{ configured: bool }`. |
| `POST` | `/api/v1/settings/claude/token` | Persists the supplied Anthropic API key encrypted at rest. Response omits the raw key. |
| `POST` | `/api/v1/settings/claude/test` | Issues a minimal Messages API request using either the supplied candidate token or the persisted token. Never overwrites the persisted value on failure. |
| `DELETE` | `/api/v1/settings/claude/token` | Removes the persisted key. Claude-backed features take their deterministic-only branch until a new key is saved. |

## Storage

The token is DataProtection-encrypted and written to
`%LOCALAPPDATA%\MealsEnPlace\settings\claude-token.dat`. The DataProtection
key ring lives under `%LOCALAPPDATA%\MealsEnPlace\keys`. Neither path is
committed to source control and both survive app restarts.

## Availability gate

`IClaudeAvailability.IsConfiguredAsync` wraps the store and is the only
signal services should consult before issuing a Claude call. On `false`,
services take their deterministic-only branch:

- `UnitOfMeasureNormalizationService` — routes unresolved tokens to the MEP-026 review queue.
- `RecipeImportService` — skips dietary classification; recipe persists with an empty tag collection.
- `RecipeMatchingService` — skips the feasibility / substitution pass and sets `ClaudeFeasibilityApplied = false` on the response.
- `MealPlanService` — skips the Claude optimization pass; deterministic ranking drives selection.

## Files

- `SettingsController.cs` — REST endpoints.
- `IClaudeTokenStore.cs` / `ClaudeTokenStore.cs` — DataProtection-backed encrypted file store.
- `ClaudeTokenStoreOptions.cs` — paths populated in `Program.cs`.
- `IClaudeAvailability.cs` / `ClaudeAvailability.cs` — the "is a key configured?" gate consumed by every Claude-touching service.
- `SaveClaudeTokenRequest.cs`, `TestClaudeTokenRequest.cs`, `ClaudeTokenStatusResponse.cs`, `ClaudeTokenTestResponse.cs` — request / response DTOs.
