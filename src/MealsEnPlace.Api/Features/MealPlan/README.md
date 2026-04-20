# Meal Plan

Generates weekly meal plans by scoring recipes against current inventory, waste-reduction priority, seasonal affinity, and variety constraints. Also owns the consume / unconsume flow for individual slots (MEP-027 / MEP-031).

## Backlog

- MEP-007 Meal Plan Generation
- MEP-027 Mark Meal as Eaten with Optional Inventory Auto-Deplete
- MEP-031 Auto-Restore Inventory When a Consumed Meal is Unmarked

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/meal-plans/generate` | Generate a new weekly meal plan |
| GET | `/api/v1/meal-plans/active` | Get the most recent meal plan |
| GET | `/api/v1/meal-plans/{id}` | Get meal plan by ID |
| GET | `/api/v1/meal-plans` | List all meal plans |
| PUT | `/api/v1/meal-plans/slots/{slotId}` | Swap a slot to a different recipe |
| POST | `/api/v1/meal-plan-slots/{id}/consume` | Mark a slot as eaten. Deducts inventory when the user's `AutoDepleteOnConsume` preference is on. |
| DELETE | `/api/v1/meal-plan-slots/{id}/consume` | Reverse a consume. Restores inventory when auto-deplete was on at consume time. |

## Key Concepts

- **Scoring**: MatchScore (inventory coverage) + WasteBonus (0.15 per expiring ingredient) + SeasonalAffinityBonus (0.1).
- **Variety**: Recipes used in the previous 7 days are excluded.
- **Slot Assignment**: Greedy algorithm assigns highest-scoring recipes to slots in chronological order.
- **Claude Optimization**: Stub passes candidates through unchanged; real implementation will optimize for variety and waste reduction. Skipped entirely when no Claude key is configured (MEP-032).
- **Default Slots**: Lunch and Dinner for each day (Monday-Sunday). Configurable via request.

## Consume / Unconsume Flow (MEP-027 / MEP-031)

- `MealPlanSlot` carries a nullable `ConsumedAt` and `ConsumedWithAutoDeplete` pair. Consuming sets both; unconsuming clears both.
- When `UserPreferences.AutoDepleteOnConsume` is on at consume time, `MealConsumptionService` deducts each recipe ingredient from inventory **oldest-expiry-first** (null expiry last) and records a `ConsumeAuditEntry` per decrement capturing the source row id, quantity, location, and expiry.
- On unconsume, the service replays each `ConsumeAuditEntry`: if the original row still exists, the deducted quantity is added back to that exact row so expiry tracking is preserved; if the row has been deleted, a new row is created from the audit's location + expiry snapshot. Audit rows are deleted after restoration.
- Insufficient inventory clamps to 0 and returns a `ShortIngredient` entry per affected ingredient — the consume still succeeds.

## Files

- `MealPlanController.cs` — Meal plan endpoints
- `MealPlanSlotsController.cs` — Slot-scoped consume / unconsume endpoints
- `IMealPlanService.cs` / `MealPlanService.cs` — Generation and management logic
- `IMealConsumptionService.cs` / `MealConsumptionService.cs` — Consume + inventory deduction + restore flow
- `MealPlanResponse.cs` — Response DTO with slots (slot response carries `ConsumedAt`)
- `ConsumeMealResponse.cs` — Response from the consume endpoint, including any short-ingredient warnings
- `GenerateMealPlanRequest.cs` — Generation parameters
- `SwapSlotRequest.cs` — Slot swap request
