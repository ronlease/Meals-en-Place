# Inventory

Manages pantry, fridge, and freezer inventory items with container reference detection.

## Backlog

- MEP-001 Inventory Management
- MEP-003 Container Reference Resolution

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/inventory` | Add item (detects container references) |
| GET | `/api/v1/inventory` | List items, optionally filtered by location |
| GET | `/api/v1/inventory/{id}` | Get single item |
| PUT | `/api/v1/inventory/{id}` | Update item |
| DELETE | `/api/v1/inventory/{id}` | Delete item |
| POST | `/api/v1/ReferenceData/ingredients` | Create canonical ingredient |
| GET | `/api/v1/ReferenceData/ingredients` | List canonical ingredients |
| GET | `/api/v1/ReferenceData/units` | List units of measure |

## Key Concepts

- **Container Reference Detection**: When adding an item, if the notes field contains a container keyword (can, jar, box, etc.) and no declared size is provided, the API returns a `ContainerReferenceDetectedResponse` instead of creating the item. The client must prompt the user for the net weight/volume and re-submit.
- **Storage Locations**: Pantry, Fridge, Freezer.
- Quantities are stored as entered by the user (no display conversion at this layer).

## Files

- `InventoryController.cs` — REST endpoints for inventory CRUD
- `ReferenceDataController.cs` — Read-only endpoints for canonical ingredients and units of measure
- `IInventoryService.cs` / `InventoryService.cs` — Business logic with container detection
- `IInventoryRepository.cs` / `InventoryRepository.cs` — EF Core data access
- `AddInventoryItemRequest.cs` — Request DTO for creating items
- `UpdateInventoryItemRequest.cs` — Request DTO for updating items
- `InventoryItemResponse.cs` — Response DTO
- `ContainerReferenceDetectedResponse.cs` — Returned when container reference detected
- `CanonicalIngredientDto.cs` — Ingredient reference data DTO
- `CreateCanonicalIngredientRequest.cs` — Request for creating ingredients
- `UnitOfMeasureDto.cs` — Unit of measure reference data DTO
