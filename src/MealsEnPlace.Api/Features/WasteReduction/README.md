# Waste Reduction

Scans inventory for items approaching expiry and matches them against recipes to generate use-it-up suggestions.

## Backlog

- MEP-009 Waste Reduction Alerts

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/waste-alerts` | Evaluate inventory and return active alerts with matched recipes |
| POST | `/api/v1/waste-alerts/{id}/dismiss` | Dismiss an alert |

## Key Concepts

- **Expiry Threshold**: 3 days until expiry triggers an alert.
- **Matched Recipes**: Each alert includes a list of fully-resolved recipes that use the expiring ingredient.
- **Soft Delete**: Dismissal sets a `DismissedAt` timestamp rather than hard-deleting the record.

## Files

- `WasteAlertController.cs` — Waste alert endpoints
- `IWasteAlertService.cs` / `WasteAlertService.cs` — Alert generation and dismissal
- `WasteAlertResponse.cs` — Response DTO with matched recipes
