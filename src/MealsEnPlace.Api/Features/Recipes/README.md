# Recipes

Recipe library management: manual creation, recipe detail / listing, container reference resolution, dietary classification, and inventory-based recipe matching. The bulk catalog is loaded offline via `MealsEnPlace.Tools.Ingest` (MEP-026); this feature covers the interactive API surface.

## Backlog

- MEP-004 Recipe Library Import — historical; original TheMealDB implementation was superseded by MEP-026 and removed under MEP-033
- MEP-005 Recipe Dietary Classification
- MEP-006 Recipe Matching
- MEP-018 Recipe Detail and Manual Recipe Management
- MEP-026 Bulk Recipe Ingest from Kaggle 2M Dataset (offline tool)
- MEP-033 Remove TheMealDB Integration

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/recipes` | List all local recipes with resolution status |
| GET | `/api/v1/recipes/{id}` | Get full recipe detail with ingredients |
| POST | `/api/v1/recipes` | Create a recipe manually |
| GET | `/api/v1/recipes/unresolved` | List recipes with unresolved container references |
| GET | `/api/v1/recipes/unresolved-groups` | List unresolved ingredients grouped for bulk resolution |
| POST | `/api/v1/recipes/unresolved-groups/resolve` | Bulk-resolve a group |
| GET | `/api/v1/recipes/{recipeId}/unresolved-ingredients` | Get unresolved ingredients for a recipe |
| PUT | `/api/v1/recipes/{recipeId}/ingredients/{ingredientId}/resolve` | Resolve a container reference |
| GET | `/api/v1/recipes/match` | Match recipes against current inventory |

## Key Concepts

- **Recipe Detail**: Full recipe view with ingredients (quantities, units of measure, resolution status), instructions, dietary tags, and optional source URL.
- **Manual Creation**: Users can create recipes directly with title, ingredients, instructions, cuisine, and serving count. Container references in notes are detected automatically.
- **Bulk Catalog**: Loaded offline through `MealsEnPlace.Tools.Ingest` (see the Tools.Ingest README). Unresolved unit-of-measure tokens are queued for human review rather than burned against Claude per occurrence.
- **Container Resolution**: Unresolved recipes do not participate in matching. User must declare net weight/volume for each container reference.
- **Recipe Matching**: Scores recipes by coverage ratio (matched/total ingredients), waste bonus for expiry-imminent items, and seasonal affinity. Results are tiered: Full Match (1.0), Near Match (>=0.75), Partial Match (>=0.5).
- **Substitution Suggestions**: When a Claude API key is configured (MEP-032), Claude reviews near-match candidates and suggests substitutions for missing ingredients. Skipped when no key is configured.

## Files

- `RecipeImportController.cs` — Manual create / list / detail endpoints
- `ContainerResolutionController.cs` — Container reference resolution endpoints
- `RecipeMatchingController.cs` — Recipe matching endpoint
- `IRecipeImportService.cs` / `RecipeImportService.cs` — Recipe CRUD for the interactive surface
- `IContainerResolutionService.cs` / `ContainerResolutionService.cs` — Container resolution logic
- `IRecipeMatchingService.cs` / `RecipeMatchingService.cs` — Matching and scoring pipeline
- DTOs: `RecipeDetailDto`, `RecipeIngredientDetailDto`, `CreateRecipeRequest`, `CreateRecipeIngredientRequest`, `RecipeListItemDto`, `RecipeMatchDto`, `RecipeMatchRequest`, `RecipeMatchResponse`, `MatchedIngredientDto`, `MissingIngredientDto`, `UnresolvedRecipeResponse`, `UnresolvedIngredientResponse`, `ResolvedIngredientResponse`, `ResolveContainerRequest`, `ContainerResolutionResult`, `MatchTier`, `UnresolvedGroupResponse`, `BulkResolveGroupRequest`, `BulkResolveGroupResponse`
