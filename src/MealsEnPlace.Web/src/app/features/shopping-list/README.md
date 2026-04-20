# Shopping List

Displays ingredients needed for the active meal plan that are missing or insufficient in inventory. Supports a one-click push to Todoist (MEP-028).

## Backlog

- MEP-008 Shopping List Derivation
- MEP-028 Push Shopping List to External Todo Provider (Todoist first)

## Route

`/shopping-list`

## Components

- **ShoppingListPageComponent** — Table showing category, ingredient name, quantity, and unit. Loads the active meal plan first, then its shopping list.
  - Regenerate button recomputes the list from the plan + current inventory.
  - "Push to Todoist" button is disabled until the Todoist integration is configured (the app reads `Todoist:Token` from user secrets today; MEP-035 will move it to the Settings page). On push, a snackbar reports counts of created / updated / closed / unchanged tasks.

## Services Used

- `MealPlanService` — Get active plan
- `ShoppingListService` — Get / generate / push shopping list
- `TodoistAvailabilityService` — Signal-backed "is Todoist configured?" gate for the push button
