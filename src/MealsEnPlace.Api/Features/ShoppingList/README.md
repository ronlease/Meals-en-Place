# Shopping List

Auto-generates shopping lists by comparing meal plan ingredient requirements against current inventory.

## Backlog

- MEP-008 Shopping List Derivation

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/meal-plans/{mealPlanId}/shopping-list` | Generate (or regenerate) the shopping list |
| GET | `/api/v1/meal-plans/{mealPlanId}/shopping-list` | Get existing shopping list |

## Key Concepts

- **Generation Flow**: Aggregates recipe requirements in base units, aggregates inventory in base units, computes deficit.
- **Display Units**: Response applies `UomDisplayConverter` for the user's preferred display system (Imperial/Metric).
- Negative deficits (surplus) are excluded from the list.

## Files

- `ShoppingListController.cs` — Shopping list endpoints
- `IShoppingListService.cs` / `ShoppingListService.cs` — Derivation logic
- `ShoppingListItemResponse.cs` — Response DTO
