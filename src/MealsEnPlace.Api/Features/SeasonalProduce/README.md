# Seasonal Produce

Provides seasonal produce guidance for USDA Zone 7a (York, PA).

## Backlog

- MEP-010 Seasonal Produce Guidance

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/seasonal-produce` | Get produce currently in season |
| GET | `/api/v1/seasonal-produce/all` | Get all seasonality windows |

## Key Concepts

- **SeasonalityWindow**: Links a canonical ingredient to its peak growing season (start/end month) and USDA zone.
- **In-Season Check**: Compares current month against the season window, handling wrap-around ranges (e.g., November-February).
- Used by recipe matching and meal plan generation for seasonal affinity scoring.

## Files

- `SeasonalProduceController.cs` — Seasonality endpoints
- `ISeasonalProduceService.cs` / `SeasonalProduceService.cs` — Query logic
- `SeasonalProduceResponse.cs` — Response DTO
