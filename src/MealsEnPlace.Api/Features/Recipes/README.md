# Recipes

Recipe library management: manual creation, recipe detail / listing, container reference resolution, dietary classification, and inventory-based recipe matching. The bulk catalog is loaded offline via `MealsEnPlace.Tools.Ingest` (MEP-026); this feature covers the interactive API surface.

## Backlog

- MEP-004 Recipe Library Import — historical; original TheMealDB implementation was superseded by MEP-026 and removed under MEP-033
- MEP-005 Recipe Dietary Classification
- MEP-006 Recipe Matching
- MEP-018 Recipe Detail and Manual Recipe Management
- MEP-026 Bulk Recipe Ingest from Kaggle 2M Dataset (offline tool)
- MEP-033 Remove TheMealDB Integration
- MEP-043 Recipe List Endpoint Pagination and Query Optimization

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/recipes` | List local recipes with pagination (`page`, `pageSize`); returns `PagedResult<RecipeListItemDto>` |
| GET | `/api/v1/recipes/{id}` | Get full recipe detail with ingredients |
| POST | `/api/v1/recipes` | Create a recipe manually |
| GET | `/api/v1/recipes/unresolved` | List recipes with unresolved container references |
| GET | `/api/v1/recipes/unresolved-groups` | List unresolved ingredients grouped for bulk resolution |
| POST | `/api/v1/recipes/unresolved-groups/resolve` | Bulk-resolve a group |
| GET | `/api/v1/recipes/{recipeId}/unresolved-ingredients` | Get unresolved ingredients for a recipe |
| PUT | `/api/v1/recipes/{recipeId}/ingredients/{ingredientId}/resolve` | Resolve a container reference |
| GET | `/api/v1/recipes/match` | Match recipes against current inventory |

### GET /api/v1/recipes — Query Parameters

| Parameter | Default | Max | Behaviour |
|-----------|---------|-----|-----------|
| `page` | 1 | — | 1-based page number; values below 1 are clamped to 1 |
| `pageSize` | 25 | 100 | Items per page; values outside [1, 100] are clamped silently |

### GET /api/v1/recipes — Response Shape (`PagedResult<RecipeListItemDto>`)

```json
{
  "items": [
    {
      "id": "guid",
      "title": "string",
      "cuisineType": "string",
      "dietaryTags": ["Vegan"],
      "isFullyResolved": true,
      "totalIngredients": 8,
      "unresolvedCount": 0
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1643098,
  "totalPages": 65724
}
```

`IngredientNames` was removed in MEP-043: it was never rendered by the recipe browser
component and was the sole reason the previous query joined the 13.6 M RecipeIngredient
rows across the full catalog, causing the 30-second Postgres command timeout.

## Key Concepts

- **Recipe Detail**: Full recipe view with ingredients (quantities, units of measure, resolution status), instructions, dietary tags, and optional source URL.
- **Manual Creation**: Users can create recipes directly with title, ingredients, instructions, cuisine, and serving count. Container references in notes are detected automatically.
- **Bulk Catalog**: Loaded offline through `MealsEnPlace.Tools.Ingest` (see the Tools.Ingest README). Unresolved unit-of-measure tokens are queued for human review rather than burned against Claude per occurrence.
- **Container Resolution**: Unresolved recipes do not participate in matching. User must declare net weight/volume for each container reference.
- **Pagination**: The recipe list endpoint uses server-side pagination (default 25, max 100 per page). Out-of-range values are clamped — no 400 errors for boundary values. Deep-offset pagination (e.g., `page=10000`) degrades at PostgreSQL's large-OFFSET cost; keyset pagination is a follow-on improvement to scope separately.
- **Recipe Matching**: Scores recipes by coverage ratio (matched/total ingredients), waste bonus for expiry-imminent items, and seasonal affinity. Results are tiered: Full Match (1.0), Near Match (>=0.75), Partial Match (>=0.5).
- **Substitution Suggestions**: When a Claude API key is configured (MEP-032), Claude reviews near-match candidates and suggests substitutions for missing ingredients. Skipped when no key is configured.

## Database Index

`IX_Recipes_Title` (B-tree) on `Recipes.Title` — added in migration `20260831013330_AddRecipesTitleIndex`. Supports `ORDER BY Title` on the paged list endpoint and keeps the parallel `COUNT(*)` affordable via an index-only scan at 1.6 M-row scale.

## Files

- `RecipeImportController.cs` — Manual create / list (paged) / detail endpoints
- `ContainerResolutionController.cs` — Container reference resolution endpoints
- `RecipeMatchingController.cs` — Recipe matching endpoint
- `IRecipeImportService.cs` / `RecipeImportService.cs` — Recipe CRUD for the interactive surface
- `IContainerResolutionService.cs` / `ContainerResolutionService.cs` — Container resolution logic
- `IRecipeMatchingService.cs` / `RecipeMatchingService.cs` — Matching and scoring pipeline
- DTOs: `RecipeDetailDto`, `RecipeIngredientDetailDto`, `CreateRecipeRequest`, `CreateRecipeIngredientRequest`, `RecipeListItemDto`, `RecipeMatchDto`, `RecipeMatchRequest`, `RecipeMatchResponse`, `MatchedIngredientDto`, `MissingIngredientDto`, `UnresolvedRecipeResponse`, `UnresolvedIngredientResponse`, `ResolvedIngredientResponse`, `ResolveContainerRequest`, `ContainerResolutionResult`, `MatchTier`, `UnresolvedGroupResponse`, `BulkResolveGroupRequest`, `BulkResolveGroupResponse`
- `Common/PagedResult.cs` — Shared pagination envelope used by this endpoint (and future paged endpoints)
