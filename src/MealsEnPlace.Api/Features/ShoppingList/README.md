# Shopping List

Auto-generates shopping lists by comparing meal plan ingredient requirements against current inventory. Supports adding recipe ingredients directly from the recipe detail view, and pushing the resulting list to an external todo provider (MEP-028).

## Backlog

- MEP-008 Shopping List Derivation
- MEP-018 Recipe Detail and Manual Recipe Management (add-to-shopping-list)
- MEP-028 Push Shopping List to External Todo Provider (Todoist first)

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/meal-plans/{mealPlanId}/shopping-list` | Generate (or regenerate) the shopping list |
| GET | `/api/v1/meal-plans/{mealPlanId}/shopping-list` | Get existing shopping list |
| POST | `/api/v1/meal-plans/{mealPlanId}/shopping-list/push/todoist` | Push this meal plan's list to Todoist. Idempotent re-push: creates new tasks, updates changed ones, closes tasks for removed items. |
| POST | `/api/v1/shopping-list/add-from-recipe/{recipeId}` | Add recipe ingredients to standalone shopping list |
| GET | `/api/v1/shopping-list` | Get standalone shopping list |
| POST | `/api/v1/shopping-list/push/todoist` | Push the standalone list to Todoist (same idempotency semantics). |

## Key Concepts

- **Generation Flow**: Aggregates recipe requirements in base units, aggregates inventory in base units, computes deficit.
- **Standalone Shopping List**: Items added directly from recipe detail views have no meal plan association. These aggregate with existing standalone items.
- **Display Units**: Response applies `UnitOfMeasureDisplayConverter` for the user's preferred display system (Imperial/Metric).
- Negative deficits (surplus) are excluded from the list.

## Todoist Push (MEP-028)

- `IShoppingListPushTarget` is the provider-agnostic push contract; `TodoistShoppingListPushTarget` is the first implementation.
- Idempotency comes from the shared `ExternalTaskLink` table (one row per pushed item, discriminated by `SourceType = ShoppingListItem` and scoped by `SourceScope` = meal plan id or `"standalone"`). Each link stores a SHA-256 hash of the task title so re-pushes can detect content changes and emit a Todoist PATCH rather than a duplicate task.
- Removed items are closed on Todoist (not deleted) so the user's completed-history stays intact.
- Token source: `Todoist:Token` via `dotnet user-secrets` for now. MEP-035 will move storage to ASP.NET DataProtection and add the Settings page paste flow; MEP-036 will surface previously-used project ids as quick-pick targets.

## Files

- `ShoppingListController.cs` — Meal-plan-scoped shopping list endpoints
- `StandaloneShoppingListController.cs` — Standalone shopping list endpoints (add from recipe)
- `ShoppingListPushController.cs` — Push endpoints (`/push/todoist`)
- `IShoppingListPushTarget.cs` — Provider abstraction; ships with `TodoistShoppingListPushTarget`
- `IShoppingListService.cs` / `ShoppingListService.cs` — Derivation and ad-hoc addition logic
- `ShoppingListItemResponse.cs` — Response DTO
