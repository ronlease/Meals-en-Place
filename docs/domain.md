# Domain

Single source for domain concepts and rules. Describes what the code does today. Feature READMEs document endpoints; backlog items document intent.

## Concepts

- **InventoryItem**: a food item on hand. Fields: location (Pantry, Fridge, Freezer), quantity, unit of measure, optional expiry date, Notes.
- **CanonicalIngredient**: normalized ingredient that inventory items and recipe ingredients map to ("Chicken Breast" regardless of brand). Has aliases and an `IngredientCategory`.
- **UnitOfMeasure**: canonical unit with a base unit and a conversion factor. `UnitOfMeasureAlias` maps colloquial tokens to units.
- **ContainerReference**: a packaging word (can, jar, box, packet, bag, bottle, carton, tube, container, envelope, package, pkg, pouch) where a unit should be. Never a unit of measure.
- **Recipe**: title, source, ingredients, instructions, cuisine, dietary tags, serving count. Bulk catalog comes from the Kaggle ingest (`MealsEnPlace.Tools.Ingest`); manual creation also exists.
- **RecipeIngredient**: recipe line with a canonical ingredient, quantity, unit, `IsContainerResolved`, and Notes (original text).
- **DietaryTag**: Carnivore, DairyFree, GlutenFree, LowCarb, Vegan, Vegetarian. A recipe carries any number.
- **MealPlan / MealPlanSlot**: weekly plan; one recipe per day and slot (Breakfast, Lunch, Dinner, Snack). Slots record consumption.
- **ShoppingListItem**: deficit between plan requirements and inventory, in a meal-plan list or a standalone list.
- **SeasonalityWindow**: peak season (start and end month) per ingredient, USDA zone 7a by default. Wrap-around ranges are supported. An ingredient with two seasons has two rows.
- **WasteAlert**: expiry notice for an inventory item with matching fully resolved recipes. Dismissal sets `DismissedAt`.
- **UserPreferences**: single row. `DisplaySystem` (Imperial default, Metric) and `AutoDepleteOnConsume`.
- **UnresolvedUnitOfMeasureToken**: unit word the ingest could not resolve deterministically. Queued for user review; see Unit resolution.

## Units

- Computation uses one base unit per type: milliliter (`ml`, Volume), gram (`g`, Weight), each (`ea`, Count). Arbitrary units do not convert and need user review.
- Stored quantities keep the unit the user or ingest supplied. Services convert to base units when they compute (`UnitOfMeasureConversionService`, `InventoryBaseHelper`).
- Cross-type conversion (cups to grams) is never attempted. It returns a not-possible result, never a guessed number.
- Display conversion runs once, at the response layer, in `UnitOfMeasureDisplayConverter`. Services never apply it. Controllers returning quantity-bearing DTOs apply it. Inventory responses show the entry unit unchanged (MEP-022).
- Imperial thresholds from base units: `ml` below 59 gives fl oz, 59 to 946 gives cups, above 946 gives qt. `g` below 454 gives oz, 454 and above gives lb. `ea` is unchanged. Metric passes base units through. No `UserPreferences` row means Imperial.

## Unit resolution

1. Deterministic: unit abbreviation or name, then `UnitOfMeasureAlias`, then count-with-ingredient-noun (resolves to `ea`).
2. Ingest path: if nothing matches, write or increment an `UnresolvedUnitOfMeasureToken` row. Claude is not called per occurrence. The user maps the token once, which creates an alias.
3. Interactive path (`NormalizeAsync`): falls back to `IClaudeService.ResolveUnitOfMeasureAsync`.
4. Claude output carries a confidence. Low confidence or Arbitrary never applies silently. The user is prompted.

Ingest parsing rules (MEP-063, MEP-067) live in `src/MealsEnPlace.Api/Features/Recipes/README.md` and the Review Queue README.

## Container resolution

Container sizes are always user-declared. No lookup table, because product sizes change without notice.

- **Inventory side**: adding an item whose notes contain a container word with no declared size returns `ContainerReferenceDetectedResponse` and does not create the item. The client asks for net weight or volume and resubmits. The item stores the declared quantity and unit, and Notes keeps the original text.
- **Recipe side**: ingest and manual create run `ContainerReferenceDetector` before unit parsing. A hit creates the `RecipeIngredient` with `IsContainerResolved = false` and the original text in Notes. The user declares the size (`PUT /api/v1/recipes/{recipeId}/ingredients/{ingredientId}/resolve`, or the bulk group endpoint). The row then stores the quantity and unit with `IsContainerResolved = true`. Notes stays unchanged.
- A recipe is fully resolved when every ingredient has `IsContainerResolved = true`. Derived, not stored.
- Detection matches whole words and plurals ("pecan" does not match "can"). The keyword list is the only detector. There is no Claude fallback.
- Unresolved recipes show an "Awaiting Resolution" badge. They get no `MatchScore`, not even 0, and are excluded from matching, meal plan candidates and waste-alert matches.
- To-taste lines ("salt", "to taste") store `Quantity = 0`, unit `ea`, `IsContainerResolved = true` and never block resolution (MEP-067).

## Recipe matching

`GET /api/v1/recipes/match` in `RecipeMatchingService`:

1. Load inventory and convert to base units, grouped by canonical ingredient.
2. Take only fully resolved recipes with at least one ingredient. Apply cuisine, dietary and season filters.
3. Per recipe: `MatchScore` = matched ingredients / total ingredients. An ingredient matches when inventory holds the same canonical ingredient in the same unit type and the summed base quantity is at least the requirement. An ingredient with no unit or a failed conversion counts as missing. A to-taste requirement (quantity 0) matches when the ingredient is in inventory.
4. Waste bonus: +0.1 per matched ingredient expiring within 3 days. `FinalScore = min(MatchScore + bonus, 1.0)`.
5. Tiers: FullMatch (`MatchScore` = 1.0), NearMatch (at least 0.75), PartialMatch (at least 0.5). Below 0.5 is dropped.
6. NearMatch candidates only: if a Claude key is configured, `SuggestSubstitutionsAsync` adds substitution notes. Errors degrade to no suggestions.
7. Rank Full, then Near, then Partial, each by `FinalScore`. Apply display conversion to quantities in the response.

## Meal plan generation

`MealPlanService`: candidates are fully resolved recipes passing dietary filters. Score = `MatchScore` + 0.15 per ingredient expiring within 3 days + 0.1 seasonal affinity. Recipes used in the previous 7 days are excluded. Greedy assignment fills slots chronologically with no duplicates in a plan. Default slots are Lunch and Dinner. When a Claude key exists, `OptimizeMealPlanAsync` reviews the full candidate set once. Its output is a suggestion and never applies silently.

Consume (MEP-027/031): `AutoDepleteOnConsume` deducts oldest expiry first and writes `ConsumeAuditEntry` rows; unconsume replays them. Reorder by expiry (MEP-030) previews before it applies.

## Shopping list

Aggregate recipe requirements in base units, subtract inventory in base units, keep positive deficits only, convert back for display. Surplus never appears as a negative. Standalone lists collect items added from recipe detail. Todoist push is idempotent through `ExternalTaskLink`.

## Waste alerts

Inventory items expiring within 3 days produce alerts listing fully resolved recipes that use the item.

## Dietary classification

`ClaudeService.ClassifyDietaryTagsAsync` is rule-based (keyword scan of ingredient names). It does not call the Claude API. Recipe import calls it. Claude-derived classification is not built.

## Claude integration

`IClaudeService` has four methods: `ClassifyDietaryTagsAsync`, `ResolveUnitOfMeasureAsync`, `OptimizeMealPlanAsync`, `SuggestSubstitutionsAsync`. `ClaudeService` is a stub (rule-based dietary tags, Low-confidence unit result, pass-through optimization). Rules for any real implementation:

- Call Claude only for work the system cannot compute deterministically.
- Request structured JSON and map it to DTOs.
- On API failure, return a degraded result with a flag. Never throw to the user.
- Low-confidence output never applies silently.
- `IClaudeAvailability.IsConfiguredAsync` gates Claude paths. Without a key the feature skips or degrades (MEP-032).

## Seasonality

`SeasonalityHelper.IsInSeason(month, start, end)` handles wrap-around. Data is seeded in migrations for zone 7a.

## Post-MVP stubs

`Features/Canning/`, `Features/StoreSales/`, `Features/Coupons/` hold a README only. No code until the backlog unblocks them.
