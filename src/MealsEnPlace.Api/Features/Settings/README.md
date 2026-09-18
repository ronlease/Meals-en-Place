# Settings

Bring-your-own Anthropic API key flow (MEP-032), the Claude model preference
(MEP-052), bring-your-own Todoist API token flow (MEP-035), Todoist project
quick-pick history (MEP-036), and the integration-status surface that
provider-dependent UI reads to disable affordances when a token is missing.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/settings/claude/status` | Returns `{ configured: bool, model: string }` for Claude. `model` is always present, key or no key. |
| `POST` | `/api/v1/settings/claude/model` | Persists the selected `ClaudeModel` (by member name, e.g. `"Sonnet5"`). 400 for an unrecognized name. Does not require a key to be configured. |
| `POST` | `/api/v1/settings/claude/token` | Persists the supplied Anthropic API key encrypted at rest. Response omits the raw key. |
| `POST` | `/api/v1/settings/claude/test` | Issues a minimal Messages API request, using the currently selected model, against either the supplied candidate token or the persisted token. Never overwrites the persisted value on failure. |
| `DELETE` | `/api/v1/settings/claude/token` | Removes the persisted Claude key. Claude-backed features take their deterministic-only branch until a new key is saved. The model preference is untouched. |
| `GET` | `/api/v1/settings/todoist/projects/history` | Returns previously-used Todoist project IDs merged with live display names (one `GET /api/v1/projects` call). Always includes the Inbox sentinel. Degrades gracefully to raw IDs with `namesResolved = false` when Todoist is unreachable or no token is configured — never returns 500 for connectivity issues. |
| `GET` | `/api/v1/settings/todoist/status` | Returns `{ configured: bool }` for Todoist; true when either the encrypted store or the legacy `Todoist:Token` user secret has a token. |
| `POST` | `/api/v1/settings/todoist/token` | Persists the supplied Todoist API token encrypted at rest. Response omits the raw token. |
| `POST` | `/api/v1/settings/todoist/test` | Issues a `GET /api/v1/projects` using either the supplied candidate token or the currently resolved token. Never overwrites the persisted value on failure. |
| `DELETE` | `/api/v1/settings/todoist/token` | Removes the persisted Todoist token. The legacy user-secret fallback (if present) remains in effect. |

## Storage

Each provider's token is DataProtection-encrypted and written to its own file
under `%LOCALAPPDATA%/MealsEnPlace/`:

| File | Purpose | DataProtection purpose |
|---|---|---|
| `claude-token.dat` | Anthropic API key | `MealsEnPlace.ClaudeToken.v1` |
| `todoist-token.dat` | Todoist personal API token | `MealsEnPlace.TodoistToken.v1` |

The Claude model preference is not a secret and is written as plain text to
`claude-model.txt` alongside the encrypted token files — a single line holding
the `ClaudeModel` member name (e.g. `Sonnet5`). Missing or unrecognized content
(a model retired in a newer app version, a hand-edited file) falls back to
`ClaudeModelCatalog.Default` (`Sonnet5`) rather than erroring the Settings page.

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

## Project history quick-pick (MEP-036)

`GET /api/v1/settings/todoist/projects/history` drives the project-picker dialog
shown before a push. It:

1. Queries `ExternalTaskLink` for distinct non-null `ExternalProjectId` values where
   `Provider = "Todoist"` (using the pre-built index on `(Provider, ExternalProjectId)`).
2. Resolves the current token via `ITodoistTokenResolver`.
3. Issues one `GET /api/v1/projects` call through `ITodoistProjectClient` to map
   IDs to display names.
4. Always prepends the Inbox sentinel (`projectId = null`, `isInbox = true`).
5. Returns `namesResolved = false` and a `nameResolutionError` message when the
   Todoist call fails or no token is configured — raw IDs remain present so the
   picker can still function.

The response also carries `lastUsedShoppingListProjectId` and
`lastUsedMealPlanProjectId`. These are derived from the most recent
`ExternalTaskLink` row per `SourceType` — no additional DB column or migration
is required. Null means no prior push for that resource type, or the last push
targeted Inbox; the dialog pre-selects Inbox in both cases.

## Per-push project override (MEP-036)

Both push endpoints accept an optional JSON body:

```json
{ "projectId": "2331547980" }
```

When `projectId` is supplied, it overrides `Todoist:ProjectId` for this push
only. The static user secret is **never** modified. The `ExternalTaskLink` rows
written by the push record the project ID actually used, so the history endpoint
reflects the chosen destination on subsequent calls.

## Model preference (MEP-052)

`ClaudeModel` enumerates the selectable model family: `Opus5`, `Sonnet5`
(default), `Haiku45`, `Fable51`. `ClaudeModelCatalog` maps each member to its
Anthropic API model ID (consumed by `AnthropicTestClient`) and display name
(consumed by the Settings page), and resolves a persisted or user-supplied
name back to the enum, falling back to `Default` for anything unrecognized.

The preference is global — one model applies to every Claude-backed call site
uniformly. `AnthropicTestClient` reads it on every `PingAsync` call, so a
Settings-page change takes effect on the next Test Connection without a
restart. The stubbed `IClaudeService` methods (see MEP-032's scope decisions)
do not yet issue real Anthropic calls, so they have nothing to wire the
preference into until they are converted to real Claude calls in a future
story — `IClaudeModelStore` is the seam they will read from at that point.

## Files

- `SettingsController.cs` — REST endpoints.
- `IClaudeTokenStore.cs` / `ClaudeTokenStore.cs` / `ClaudeTokenStoreOptions.cs` — Claude token encrypted-file store.
- `IClaudeAvailability.cs` / `ClaudeAvailability.cs` — the "is a Claude key configured?" gate.
- `ClaudeModel.cs` / `ClaudeModelCatalog.cs` — the selectable model enum and its ID/display-name/parsing catalog.
- `IClaudeModelStore.cs` / `ClaudeModelStore.cs` / `ClaudeModelStoreOptions.cs` — Claude model preference plain-text file store.
- `SaveClaudeModelRequest.cs` — model-selection DTO.
- `ITodoistProjectHistoryService.cs` / `TodoistProjectHistoryService.cs` — assembles the project quick-pick response.
- `ITodoistTokenStore.cs` / `TodoistTokenStore.cs` / `TodoistTokenStoreOptions.cs` — Todoist token encrypted-file store.
- `SaveClaudeTokenRequest.cs`, `TestClaudeTokenRequest.cs`, `ClaudeTokenStatusResponse.cs`, `ClaudeTokenTestResponse.cs` — Claude DTOs.
- `SaveTodoistTokenRequest.cs`, `TestTodoistTokenRequest.cs`, `TodoistStatusResponse.cs`, `TodoistTokenTestResponse.cs` — Todoist token-management DTOs.
- `TodoistProjectHistoryEntry.cs`, `TodoistProjectHistoryResponse.cs` — project history DTOs.
