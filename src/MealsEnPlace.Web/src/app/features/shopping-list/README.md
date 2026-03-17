# Shopping List

Displays ingredients needed for the active meal plan that are missing or insufficient in inventory.

## Backlog

- MEP-008 Shopping List Derivation

## Route

`/shopping-list`

## Components

- **ShoppingListPageComponent** — Table showing category, ingredient name, quantity, and unit. Loads the active meal plan first, then its shopping list. Includes regenerate button and empty-state messaging.

## Services Used

- `MealPlanService` — Get active plan
- `ShoppingListService` — Get and generate shopping list
