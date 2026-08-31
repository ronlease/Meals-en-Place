# Recipes

Recipe library browser, manual recipe creation, recipe detail dialog, container-resolution flow, and inventory-based recipe matching with three-tier results. The bulk recipe catalog is loaded offline via `MealsEnPlace.Tools.Ingest` (MEP-026); this feature covers the interactive flows.

## Backlog

- MEP-004 Recipe Library Import — historical; TheMealDB implementation superseded by MEP-026 and removed under MEP-033
- MEP-005 Recipe Dietary Classification
- MEP-006 Recipe Matching
- MEP-018 Recipe Detail and Manual Recipe Management
- MEP-033 Remove TheMealDB Integration
- MEP-043 Recipe List Endpoint Pagination and Query Optimization

## Routes

- `/recipes` — Recipe browser (library + "What Can I Make?")
- `/recipes/create` — Manual recipe creation form
- `/recipes/container-resolution` — Unresolved container reference groups with bulk resolution

## Components

- **RecipeBrowserComponent** — Two-tab interface: "My Recipes" table (click row to open detail dialog) with resolution status badges, and "What Can I Make?" match finder with dietary tag chip filters.
  - Paging: server-side, 25 recipes per page. The paginator exposes **previous/next navigation only** — first/last-page buttons and the page-size selector are suppressed via `showFirstLastButtons` (default false) and `[hidePageSize]="true"`. This is intentional: at 1.6 M+ recipes the last-page position takes ~20 s to query and pages beyond ~1 500 exceed the database command timeout. Offering a one-click jump to those positions would guarantee timeouts.
- **RecipeDetailDialogComponent** — Dialog showing full recipe detail: ingredients table, instructions, dietary tags, source URL link, and "Add to Shopping List" button.
- **RecipeCreateComponent** — Form for manual recipe creation with dynamic ingredient rows, ingredient/unit of measure selection, and container reference notes.
- **ContainerResolutionPageComponent** — Grouped view of unresolved container references with a bulk-resolve dialog.
- **RecipeMatchResultsComponent** — Presentational component showing Full/Near/Partial match tiers.

## Services Used

- `RecipeService` — Library listing (server-side paged via `GET /api/v1/recipes?page=&pageSize=`), detail, creation, matching, unresolved-group bulk resolve, add-to-shopping-list
- `ReferenceDataService` — Canonical ingredients and unit of measure lookup (for recipe creation form)

## Models

- `PagedResult<T>` (in `core/models/recipe.models.ts`) — Generic wrapper matching the API's paged envelope: `items`, `page`, `pageSize`, `totalCount`, `totalPages`.
