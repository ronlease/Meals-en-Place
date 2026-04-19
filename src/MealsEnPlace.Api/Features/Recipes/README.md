# Recipes

Recipe library management: import from TheMealDB, manual creation, recipe detail, container reference resolution, dietary classification, and inventory-based recipe matching.

## Backlog

- MEP-004 Recipe Library Import
- MEP-005 Recipe Dietary Classification
- MEP-006 Recipe Matching
- MEP-018 Recipe Detail and Manual Recipe Management

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/recipes` | List all local recipes with resolution status |
| GET | `/api/v1/recipes/{id}` | Get full recipe detail with ingredients |
| POST | `/api/v1/recipes` | Create a recipe manually |
| POST | `/api/v1/recipes/import/{mealDbId}` | Import recipe from TheMealDB |
| GET | `/api/v1/recipes/search?query=` | Search TheMealDB by name |
| GET | `/api/v1/recipes/search/category?category=` | Search TheMealDB by category |
| GET | `/api/v1/recipes/unresolved` | List recipes with unresolved container references |
| GET | `/api/v1/recipes/{recipeId}/unresolved-ingredients` | Get unresolved ingredients for a recipe |
| PUT | `/api/v1/recipes/{recipeId}/ingredients/{ingredientId}/resolve` | Resolve a container reference |
| GET | `/api/v1/recipes/match` | Match recipes against current inventory |

## Key Concepts

- **Recipe Detail**: Full recipe view with ingredients (quantities, units of measure, resolution status), instructions, dietary tags, and source URL for imported recipes.
- **Manual Creation**: Users can create recipes directly with title, ingredients, instructions, cuisine, and serving count. Container references in notes are detected automatically.
- **Import Pipeline**: Search TheMealDB, import by ID, detect container references in ingredients, classify dietary tags via Claude stub.
- **Container Resolution**: Unresolved recipes do not participate in matching. User must declare net weight/volume for each container reference.
- **Recipe Matching**: Scores recipes by coverage ratio (matched/total ingredients), waste bonus for expiry-imminent items, and seasonal affinity. Results are tiered: Full Match (1.0), Near Match (>=0.75), Partial Match (>=0.5).
- **Substitution Suggestions**: Claude reviews near-match candidates and suggests substitutions for missing ingredients.

## Files

- `RecipeImportController.cs` — Search/import/list/detail/create endpoints
- `ContainerResolutionController.cs` — Container reference resolution endpoints
- `RecipeMatchingController.cs` — Recipe matching endpoint
- `IRecipeImportService.cs` / `RecipeImportService.cs` — Recipe CRUD and TheMealDB import pipeline
- `IContainerResolutionService.cs` / `ContainerResolutionService.cs` — Container resolution logic
- `IRecipeMatchingService.cs` / `RecipeMatchingService.cs` — Matching and scoring pipeline
- DTOs: `RecipeDetailDto`, `RecipeIngredientDetailDto`, `CreateRecipeRequest`, `CreateRecipeIngredientRequest`, `RecipeListItemDto`, `RecipeSearchResultDto`, `RecipeImportResultDto`, `RecipeMatchDto`, `RecipeMatchRequest`, `RecipeMatchResponse`, `MatchedIngredientDto`, `MissingIngredientDto`, `UnresolvedRecipeResponse`, `UnresolvedIngredientResponse`, `ResolvedIngredientResponse`, `ResolveContainerRequest`, `ContainerResolutionResult`, `MatchTier`
