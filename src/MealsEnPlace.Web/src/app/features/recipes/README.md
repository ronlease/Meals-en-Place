# Recipes

Recipe library browser, TheMealDB import workflow, manual recipe creation, recipe detail dialog, and inventory-based recipe matching with three-tier results.

## Backlog

- MEP-004 Recipe Library Import
- MEP-005 Recipe Dietary Classification
- MEP-006 Recipe Matching
- MEP-018 Recipe Detail and Manual Recipe Management

## Routes

- `/recipes` — Recipe browser (library + "What Can I Make?")
- `/recipes/create` — Manual recipe creation form
- `/recipes/import` — TheMealDB search and import

## Components

- **RecipeBrowserComponent** — Two-tab interface: "My Recipes" table (click row to open detail dialog) with resolution status badges, and "What Can I Make?" match finder with dietary tag chip filters.
- **RecipeDetailDialogComponent** — Dialog showing full recipe detail: ingredients table, instructions, dietary tags, source URL link, and "Add to Shopping List" button.
- **RecipeCreateComponent** — Form for manual recipe creation with dynamic ingredient rows, ingredient/unit of measure selection, and container reference notes.
- **RecipeImportComponent** — Search bar + result card grid with import status tracking.
- **RecipeMatchResultsComponent** — Presentational component showing Full/Near/Partial match tiers.

## Services Used

- `RecipeService` — Library listing, detail, creation, import, search, matching, add-to-shopping-list
- `ReferenceDataService` — Canonical ingredients and unit of measure lookup (for recipe creation form)
