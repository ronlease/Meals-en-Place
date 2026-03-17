# Meal Plan

Generates weekly meal plans by scoring recipes against current inventory, waste-reduction priority, seasonal affinity, and variety constraints.

## Backlog

- MEP-007 Meal Plan Generation

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/meal-plans/generate` | Generate a new weekly meal plan |
| GET | `/api/v1/meal-plans/active` | Get the most recent meal plan |
| GET | `/api/v1/meal-plans/{id}` | Get meal plan by ID |
| GET | `/api/v1/meal-plans` | List all meal plans |
| PUT | `/api/v1/meal-plans/slots/{slotId}` | Swap a slot to a different recipe |

## Key Concepts

- **Scoring**: MatchScore (inventory coverage) + WasteBonus (0.15 per expiring ingredient) + SeasonalAffinityBonus (0.1).
- **Variety**: Recipes used in the previous 7 days are excluded.
- **Slot Assignment**: Greedy algorithm assigns highest-scoring recipes to slots in chronological order.
- **Claude Optimization**: Stub passes candidates through unchanged; real implementation will optimize for variety and waste reduction.
- **Default Slots**: Lunch and Dinner for each day (Monday-Sunday). Configurable via request.

## Files

- `MealPlanController.cs` — Meal plan endpoints
- `IMealPlanService.cs` / `MealPlanService.cs` — Generation and management logic
- `MealPlanResponse.cs` — Response DTO with slots
- `GenerateMealPlanRequest.cs` — Generation parameters
- `SwapSlotRequest.cs` — Slot swap request
