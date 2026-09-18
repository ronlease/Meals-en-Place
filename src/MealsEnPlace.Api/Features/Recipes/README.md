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
- MEP-046 Recipe Search and Filtering

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
| `dietaryTag` | _(none)_ | — | Repeatable enum filter (`?dietaryTag=Vegetarian&dietaryTag=GlutenFree`). Only recipes carrying **all** specified tags are returned (AND semantics). Values: `Carnivore`, `DairyFree`, `GlutenFree`, `LowCarb`, `Vegan`, `Vegetarian`. |
| `ingredient` | _(none)_ | — | Case-insensitive substring match against canonical ingredient names. Only recipes containing at least one matching ingredient are returned. Backed by a `pg_trgm` GIN index. |
| `page` | 1 | — | 1-based page number; values below 1 are clamped to 1 |
| `pageSize` | 25 | 100 | Items per page; values outside [1, 100] are clamped silently |
| `q` | _(none)_ | — | Case-insensitive substring match against recipe titles. Omit or leave empty to return unfiltered paginated results (identical to MEP-043 behaviour). Backed by a `pg_trgm` GIN index. |

All filter parameters combine with AND semantics — e.g. `?q=pasta&dietaryTag=Vegetarian&ingredient=tomato` returns only recipes whose title contains "pasta", that are tagged Vegetarian, and that contain an ingredient named something like "tomato".

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

## Database Indexes

| Index | Table | Type | Purpose |
|-------|-------|------|---------|
| `IX_Recipes_Title` | `Recipes` | B-tree | `ORDER BY Title` on the paged list; keeps `COUNT(*)` affordable via index-only scan at 1.6 M-row scale. Added in `20260831013330_AddRecipesTitleIndex`. |
| `IX_Recipes_Title_Trgm` | `Recipes` | GIN (pg_trgm) | Accelerates `ILIKE '%term%'` title search (`?q=`). Added in `20260918021211_AddRecipeSearchTrigrams`. |
| `IX_CanonicalIngredients_Name_Trgm` | `CanonicalIngredients` | GIN (pg_trgm) | Accelerates `ILIKE '%term%'` ingredient-name search (`?ingredient=`). Added in `20260918021211_AddRecipeSearchTrigrams`. |

The `pg_trgm` PostgreSQL extension is enabled by migration `20260918021211_AddRecipeSearchTrigrams` (`CREATE EXTENSION IF NOT EXISTS pg_trgm`).

## Files

- `RecipeImportController.cs` — Manual create / list (paged) / detail endpoints
- `ContainerResolutionController.cs` — Container reference resolution endpoints
- `RecipeMatchingController.cs` — Recipe matching endpoint
- `IRecipeImportService.cs` / `RecipeImportService.cs` — Recipe CRUD for the interactive surface
- `IContainerResolutionService.cs` / `ContainerResolutionService.cs` — Container resolution logic
- `IRecipeMatchingService.cs` / `RecipeMatchingService.cs` — Matching and scoring pipeline
- DTOs: `RecipeDetailDto`, `RecipeIngredientDetailDto`, `CreateRecipeRequest`, `CreateRecipeIngredientRequest`, `RecipeListItemDto`, `RecipeSearchQuery`, `RecipeMatchDto`, `RecipeMatchRequest`, `RecipeMatchResponse`, `MatchedIngredientDto`, `MissingIngredientDto`, `UnresolvedRecipeResponse`, `UnresolvedIngredientResponse`, `ResolvedIngredientResponse`, `ResolveContainerRequest`, `ContainerResolutionResult`, `MatchTier`, `UnresolvedGroupResponse`, `BulkResolveGroupRequest`, `BulkResolveGroupResponse`
- `Common/PagedResult.cs` — Shared pagination envelope used by this endpoint (and future paged endpoints)
