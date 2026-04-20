# Meal Plan

Generates weekly meal plans by scoring recipes against current inventory, waste-reduction priority, seasonal affinity, and variety constraints. Also owns the consume / unconsume flow for individual slots (MEP-027 / MEP-031), the expiry-driven reorder (MEP-030), and the Todoist push (MEP-029).

## Backlog

- MEP-007 Meal Plan Generation
- MEP-027 Mark Meal as Eaten with Optional Inventory Auto-Deplete
- MEP-029 Push Meal Plan to External Todo Provider (Todoist first)
- MEP-030 Reorder Meal Plan to Prioritize Expiring Ingredients
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
| POST | `/api/v1/meal-plans/{id}/reorder-by-expiry/preview` | Compute a proposed reorder of slot days based on expiring ingredients. Does not mutate. Accepts optional `?urgencyWindowDays=` (default 7). |
| POST | `/api/v1/meal-plans/{id}/reorder-by-expiry/apply` | Commit the previewed reorder — permutes slot `DayOfWeek` within each `MealSlot`. |
| POST | `/api/v1/meal-plans/{id}/push/todoist` | Push every slot as a scheduled Todoist task (MEP-029). Idempotent on re-push: creates, updates, and closes tasks as the plan changes. |

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

## Reorder by Expiry (MEP-030)

- `MealPlanReorderService` computes a per-slot urgency score from the current inventory snapshot: for each recipe ingredient, score = `max(0, urgencyWindow − daysUntilExpiry) / urgencyWindow`. Already-expired ingredients contribute 1.0 (maximum urgency). Ingredients outside the window contribute 0.
- The reorder permutes `DayOfWeek` **within each `MealSlot`** — Breakfast recipes shuffle among Breakfast slots, Lunch among Lunch, etc. Cross-meal-type reordering is intentionally out of scope (the AC flagged it TBD) so the user's meal-rhythm is preserved.
- Tie-break uses the original `DayOfWeek` ordering so equal-urgency recipes retain their relative sequence (stable sort).
- Preview returns a `ReorderPreviewResponse` with per-slot before/after day assignments and a `Reason` string when no changes are recommended. Apply persists the new assignments and returns the refreshed plan.

## Todoist Push (MEP-029)

- `IMealPlanPushTarget` defines the per-resource provider contract; `TodoistMealPlanPushTarget` is the first implementation.
- One Todoist task per `MealPlanSlot` titled `"{MealSlot}: {RecipeTitle}"` with a `due_date` computed from `WeekStartDate` + the slot's `DayOfWeek` offset (Monday-first ordering, matching the generation code).
- Idempotency via the shared `ExternalTaskLink` table (discriminated by `SourceType = MealPlanSlot`, scoped by meal plan id). Hash input includes both content and due date so a recipe swap *or* a day shuffle shows up as a stale hash and triggers an update.
- Removed slots → remote task close + local link delete. Slot recipe swap → PATCH (update).
- Token source and follow-ons: same as MEP-028 — `Todoist:Token` user secret for now; MEP-035 moves storage to DataProtection and adds the Settings UI; MEP-036 adds project-id quick-pick.

## Files

- `MealPlanController.cs` — Meal plan endpoints (including reorder preview / apply)
- `MealPlanSlotsController.cs` — Slot-scoped consume / unconsume endpoints
- `MealPlanPushController.cs` — Push endpoint (`/push/todoist`)
- `IMealPlanService.cs` / `MealPlanService.cs` — Generation and management logic
- `IMealConsumptionService.cs` / `MealConsumptionService.cs` — Consume + inventory deduction + restore flow
- `IMealPlanReorderService.cs` / `MealPlanReorderService.cs` — Preview + apply expiry-driven reorder
- `IMealPlanPushTarget.cs` — Push provider abstraction; ships with `TodoistMealPlanPushTarget`
- `MealPlanResponse.cs` — Response DTO with slots (slot response carries `ConsumedAt`)
- `ConsumeMealResponse.cs` — Response from the consume endpoint, including any short-ingredient warnings
- `ReorderPreviewResponse.cs` — Response from the reorder preview endpoint
- `GenerateMealPlanRequest.cs` — Generation parameters
- `SwapSlotRequest.cs` — Slot swap request
