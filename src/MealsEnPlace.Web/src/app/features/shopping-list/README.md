# Shopping List

Displays ingredients needed for the active meal plan that are missing or insufficient in inventory. Supports a one-click push to Todoist (MEP-028).

## Backlog

- MEP-008 Shopping List Derivation
- MEP-028 Push Shopping List to External Todo Provider (Todoist first)
- MEP-036 Surface Associated Todoist Project IDs for Push Target Selection

## Route

`/shopping-list`

## Components

- **ShoppingListPageComponent** — Table showing category, ingredient name, quantity, and unit. Loads the active meal plan first, then its shopping list.
  - Regenerate button recomputes the list from the plan + current inventory.
  - "Push to Todoist" button is disabled until the Todoist integration is configured — save a token on the Settings page (MEP-035); the legacy `Todoist:Token` user secret still works as a fallback.
  - Clicking "Push to Todoist" opens the `TodoistProjectPickerDialogComponent` (shared) before the push executes. Dismissing the dialog performs no push; confirming pushes to the selected project.
  - The dialog pre-selects the project that was last used from this surface. The server derives the last-used project per resource type from `ExternalTaskLink` push history and returns it in the history response (`lastUsedShoppingListProjectId`). The shopping-list and meal-plan surfaces remember independently; no client-side storage is involved.
  - On push, a snackbar reports counts of created / updated / closed / unchanged tasks.

## Services Used

- `MealPlanService` — Get active plan
- `ShoppingListService` — Get / generate / push shopping list (push now accepts `projectId: string | null`)
- `SettingsService` — `getProjectHistory()` (called by the picker dialog) fetches previously-used Todoist projects from `GET /api/v1/settings/todoist/projects/history`
- `TodoistAvailabilityService` — Signal-backed "is Todoist configured?" gate for the push button
