# Meal Plan

Weekly meal plan board with generation, recipe swap, consume / unconsume (MEP-027 / MEP-031), expiry-driven reorder (MEP-030), and Todoist push (MEP-029).

## Backlog

- MEP-007 Meal Plan Generation
- MEP-027 Mark Meal as Eaten with Optional Inventory Auto-Deplete
- MEP-029 Push Meal Plan to External Todo Provider (Todoist first)
- MEP-030 Reorder Meal Plan to Prioritize Expiring Ingredients
- MEP-031 Auto-Restore Inventory When a Consumed Meal is Unmarked

## Route

`/meal-plan`

## Components

- **MealPlanBoardComponent** — 7-column grid (days of week) with meal slots as cards.
  - Click the card body to open the swap dialog.
  - Each card has a "Mark eaten" / "Unmark" action button. Consumed slots render with a green check and muted / strikethrough styling.
  - After consume, a snackbar reports any short ingredients surfaced by the backend (`ShortIngredientResponse[]`).
  - A "Reorder by expiry" action in the page header opens the reorder preview dialog.
  - A "Push to Todoist" action in the page header is disabled until Todoist is configured (`Todoist:Token` user secret today; MEP-035 later). On push, a snackbar reports counts of created / updated / closed / unchanged tasks.
- **MealPlanGenerateDialogComponent** — Form with plan name, seasonal preference checkbox.
- **MealPlanReorderDialogComponent** — Side-by-side before/after day assignments with Confirm / Cancel. Shows the urgency score per changed slot and the urgency window used.
- **MealPlanSwapDialogComponent** — Recipe selection list filtered to exclude current and unresolved recipes.

## Services Used

- `MealPlanService` — Get active plan, generate, swap slot, `consumeSlot`, `unconsumeSlot`, `previewReorderByExpiry`, `applyReorderByExpiry`, `pushToTodoist`
- `RecipeService` — Load recipes for swap dialog
- `PreferencesService` — Reads the current `autoDepleteOnConsume` signal to describe the current behavior in the settings page
- `TodoistAvailabilityService` — Signal-backed gate for the push button
