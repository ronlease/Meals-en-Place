# Inventory

Pantry, fridge, and freezer inventory management with tabbed layout, add/edit dialogs, and container reference detection.

## Backlog

- MEP-001 Inventory Management
- MEP-003 Container Reference Resolution
- MEP-048 Server-Side Ingredient Search for the Inventory Dialog Autocomplete

## Route

`/inventory` (default landing page)

## Components

- **InventoryPageComponent** — Tabbed container (Pantry / Fridge / Freezer) with Add button
- **InventoryTableComponent** — Reusable table for each location with edit/delete actions and expiry badges
- **InventoryDialogComponent** — Add/edit modal with server-side ingredient search (via `IngredientAutocompleteComponent`), unit of measure picker, expiry date, and container reference detection prompt

## Services Used

- `InventoryService` — CRUD operations
- `ReferenceDataService` — Units of measure lookup; ingredient search is debounced and server-side via `GET /api/v1/referencedata/ingredients?search=<term>`

## Ingredient Autocomplete

Ingredient lookup no longer pre-loads the full ingredient catalogue. The shared
`IngredientAutocompleteComponent` drives a debounced `rxResource` that calls
`ReferenceDataService.searchIngredients()` only after the user has typed at least
two characters and a 250 ms debounce period has elapsed. Stale in-flight requests
are cancelled automatically. The full ingredient list is never loaded.
