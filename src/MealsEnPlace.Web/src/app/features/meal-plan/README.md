# Meal Plan

Weekly meal plan board with generation, recipe swap, consume / unconsume (MEP-027 / MEP-031), expiry-driven reorder (MEP-030), and Todoist push (MEP-029 / MEP-036).

## Backlog

- MEP-007 Meal Plan Generation
- MEP-027 Mark Meal as Eaten with Optional Inventory Auto-Deplete
- MEP-029 Push Meal Plan to External Todo Provider (Todoist first)
- MEP-030 Reorder Meal Plan to Prioritize Expiring Ingredients
- MEP-031 Auto-Restore Inventory When a Consumed Meal is Unmarked
- MEP-036 Surface Associated Todoist Project IDs for Push Target Selection

## Route

`/meal-plan`

## Components

- **MealPlanBoardComponent** — 7-column grid (days of week) with meal slots as cards.
  - Click the card body to open the swap dialog.
  - Each card has a "Mark eaten" / "Unmark" action button. Consumed slots render with a green check and muted / strikethrough styling.
  - After consume, a snackbar reports any short ingredients surfaced by the backend (`ShortIngredientResponse[]`).
  - A "Reorder by expiry" action in the page header opens the reorder preview dialog.
  - "Push to Todoist" in the page header is disabled until Todoist is configured — save a token on the Settings page (MEP-035). Clicking it opens the `TodoistProjectPickerDialogComponent` (shared) before the push executes. Dismissing performs no push; confirming pushes to the selected project. The dialog pre-selects the last-used project for this surface, derived server-side from `ExternalTaskLink` push history and returned in the history response as `lastUsedMealPlanProjectId` — independent from the shopping-list surface, and no client-side storage is involved. On push, a snackbar reports counts of created / updated / closed / unchanged tasks.
- **MealPlanGenerateDialogComponent** — Form with plan name, seasonal preference checkbox.
- **MealPlanReorderDialogComponent** — Side-by-side before/after day assignments with Confirm / Cancel. Shows the urgency score per changed slot and the urgency window used.
- **MealPlanSwapDialogComponent** — Recipe selection list filtered to exclude current and unresolved recipes.

## Shared Components Used

- **TodoistProjectPickerDialogComponent** (`shared/todoist-project-picker/`) — History-based Todoist project picker shown before any push executes. Fetches previously-used projects from `GET /api/v1/settings/todoist/projects/history`. Always includes "Inbox (default)". Degrades gracefully to raw project IDs when names cannot be resolved.

## Services Used

- `MealPlanService` — Get active plan, generate, swap slot, `consumeSlot`, `unconsumeSlot`, `previewReorderByExpiry`, `applyReorderByExpiry`, `pushToTodoist` (now accepts `projectId: string | null`)
- `RecipeService` — Load recipes for swap dialog
- `PreferencesService` — Reads the current `autoDepleteOnConsume` signal to describe the current behavior in the settings page
- `SettingsService` — `getProjectHistory()` (called by the picker dialog) fetches previously-used Todoist projects
- `TodoistAvailabilityService` — Signal-backed gate for the push button
