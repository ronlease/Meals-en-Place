# Recipes

Recipe library browser, TheMealDB import workflow, and inventory-based recipe matching with three-tier results.

## Backlog

- MEP-004 Recipe Library Import
- MEP-005 Recipe Dietary Classification
- MEP-006 Recipe Matching

## Routes

- `/recipes` — Recipe browser (library + "What Can I Make?")
- `/recipes/import` — TheMealDB search and import

## Components

- **RecipeBrowserComponent** — Two-tab interface: "My Recipes" table with resolution status badges, and "What Can I Make?" match finder with dietary tag chip filters.
- **RecipeImportComponent** — Search bar + result card grid with import status tracking (spinner, done, conflict, error).
- **RecipeMatchResultsComponent** — Presentational component showing Full/Near/Partial match tiers with ingredient coverage, missing ingredients, and substitution suggestions.

## Services Used

- `RecipeService` — Library listing, import, search, matching
