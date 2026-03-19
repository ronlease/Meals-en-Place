# Shopping List

Auto-generates shopping lists by comparing meal plan ingredient requirements against current inventory. Also supports adding recipe ingredients directly from the recipe detail view.

## Backlog

- MEP-008 Shopping List Derivation
- MEP-018 Recipe Detail and Manual Recipe Management (add-to-shopping-list)

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/meal-plans/{mealPlanId}/shopping-list` | Generate (or regenerate) the shopping list |
| GET | `/api/v1/meal-plans/{mealPlanId}/shopping-list` | Get existing shopping list |
| POST | `/api/v1/shopping-list/add-from-recipe/{recipeId}` | Add recipe ingredients to standalone shopping list |
| GET | `/api/v1/shopping-list` | Get standalone shopping list |

## Key Concepts

- **Generation Flow**: Aggregates recipe requirements in base units, aggregates inventory in base units, computes deficit.
- **Standalone Shopping List**: Items added directly from recipe detail views have no meal plan association. These aggregate with existing standalone items.
- **Display Units**: Response applies `UomDisplayConverter` for the user's preferred display system (Imperial/Metric).
- Negative deficits (surplus) are excluded from the list.

## Files

- `ShoppingListController.cs` — Meal-plan-scoped shopping list endpoints
- `StandaloneShoppingListController.cs` — Standalone shopping list endpoints (add from recipe)
- `IShoppingListService.cs` / `ShoppingListService.cs` — Derivation and ad-hoc addition logic
- `ShoppingListItemResponse.cs` — Response DTO
