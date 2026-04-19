# Preferences

User preferences management — currently supports display unit system (Imperial/Metric).

## Backlog

- MEP-011 Metric Display

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/preferences` | Get current preferences |
| PUT | `/api/v1/preferences` | Update display system preference |

## Key Concepts

- **DisplaySystem**: Imperial (default) or Metric. Controls how quantities render in API responses.
- **Single-row table**: UserPreferences is enforced as a single-row table with a fixed primary key.
- **UnitOfMeasureDisplayConverter**: Reads the preference and converts base metric units to the user's chosen display system.

## Files

- `UserPreferencesController.cs` — GET/PUT endpoints
- `UserPreferencesResponse.cs` — Response DTO
- `UpdateUserPreferencesRequest.cs` — Request DTO
