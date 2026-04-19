# Inventory

Pantry, fridge, and freezer inventory management with tabbed layout, add/edit dialogs, and container reference detection.

## Backlog

- MEP-001 Inventory Management
- MEP-003 Container Reference Resolution

## Route

`/inventory` (default landing page)

## Components

- **InventoryPageComponent** — Tabbed container (Pantry / Fridge / Freezer) with Add button
- **InventoryTableComponent** — Reusable table for each location with edit/delete actions and expiry badges
- **InventoryDialogComponent** — Add/edit modal with autocomplete ingredient selector, unit of measure picker, expiry date, and container reference detection prompt

## Services Used

- `InventoryService` — CRUD operations
- `ReferenceDataService` — Canonical ingredients and unit of measure lookup
