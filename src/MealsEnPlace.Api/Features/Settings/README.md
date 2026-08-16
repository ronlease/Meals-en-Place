# Settings

Bring-your-own Anthropic API key flow (MEP-032), bring-your-own Todoist API
token flow (MEP-035), and the integration-status surface that provider-dependent
UI reads to disable affordances when a token is missing.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/settings/claude/status` | Returns `{ configured: bool }` for Claude. |
| `POST` | `/api/v1/settings/claude/token` | Persists the supplied Anthropic API key encrypted at rest. Response omits the raw key. |
| `POST` | `/api/v1/settings/claude/test` | Issues a minimal Messages API request using either the supplied candidate token or the persisted token. Never overwrites the persisted value on failure. |
| `DELETE` | `/api/v1/settings/claude/token` | Removes the persisted Claude key. Claude-backed features take their deterministic-only branch until a new key is saved. |
| `GET` | `/api/v1/settings/todoist/status` | Returns `{ configured: bool }` for Todoist; true when either the encrypted store or the legacy `Todoist:Token` user secret has a token. |
| `POST` | `/api/v1/settings/todoist/token` | Persists the supplied Todoist API token encrypted at rest. Response omits the raw token. |
| `POST` | `/api/v1/settings/todoist/test` | Issues a `GET /rest/v2/projects` using either the supplied candidate token or the currently resolved token. Never overwrites the persisted value on failure. |
| `DELETE` | `/api/v1/settings/todoist/token` | Removes the persisted Todoist token. The legacy user-secret fallback (if present) remains in effect. |

## Storage

Each provider's token is DataProtection-encrypted and written to its own file
under `%LOCALAPPDATA%/MealsEnPlace/`:

| File | Purpose | DataProtection purpose |
|---|---|---|
| `claude-token.dat` | Anthropic API key | `MealsEnPlace.ClaudeToken.v1` |
| `todoist-token.dat` | Todoist personal API token | `MealsEnPlace.TodoistToken.v1` |

The DataProtection key ring is shared across providers and lives at
`%LOCALAPPDATA%/MealsEnPlace/keys/`. Distinct purpose strings mean ciphertexts
are not interchangeable — a file swap cannot leak one provider's token to
another. Neither the token files nor the key ring are committed to source
control, and both survive app restarts.

Cross-platform path note: `%LOCALAPPDATA%` resolves to
`$HOME/.local/share/MealsEnPlace/` on macOS and Linux (via
`Environment.SpecialFolder.LocalApplicationData`). At-rest encryption of the
key ring itself is platform-dependent — Windows wraps the ring with DPAPI,
while macOS and Linux rely on filesystem permissions. Hardening that gap is
tracked in MEP-039 (post-MVP).

## Availability gates

### Claude

`IClaudeAvailability.IsConfiguredAsync` wraps the Claude token store and is the
only signal services should consult before issuing a Claude call. On `false`,
services take their deterministic-only branch:

- `UnitOfMeasureNormalizationService` — routes unresolved tokens to the MEP-026 review queue.
- `RecipeImportService` — skips dietary classification; recipe persists with an empty tag collection.
- `RecipeMatchingService` — skips the feasibility / substitution pass and sets `ClaudeFeasibilityApplied = false` on the response.
- `MealPlanService` — skips the Claude optimization pass; deterministic ranking drives selection.

### Todoist

`ITodoistTokenResolver` is the single source of truth for "is Todoist reachable?".
It returns the encrypted-store value when present and falls back to the
`Todoist:Token` user secret otherwise. Consumers:

- `TodoistClient` — resolves the token on each request so a Settings-page update takes effect without a restart.
- `TodoistShoppingListPushTarget` / `TodoistMealPlanPushTarget` — preflight via `HasTokenAsync` and throw with a friendly "configure from Settings" message when empty.
- `SettingsController.GetTodoistStatus` — returns `configured = HasTokenAsync()`.

## Files

- `SettingsController.cs` — REST endpoints.
- `IClaudeTokenStore.cs` / `ClaudeTokenStore.cs` / `ClaudeTokenStoreOptions.cs` — Claude token encrypted-file store.
- `IClaudeAvailability.cs` / `ClaudeAvailability.cs` — the "is a Claude key configured?" gate.
- `ITodoistTokenStore.cs` / `TodoistTokenStore.cs` / `TodoistTokenStoreOptions.cs` — Todoist token encrypted-file store.
- `SaveClaudeTokenRequest.cs`, `TestClaudeTokenRequest.cs`, `ClaudeTokenStatusResponse.cs`, `ClaudeTokenTestResponse.cs` — Claude DTOs.
- `SaveTodoistTokenRequest.cs`, `TestTodoistTokenRequest.cs`, `TodoistStatusResponse.cs`, `TodoistTokenTestResponse.cs` — Todoist DTOs.
