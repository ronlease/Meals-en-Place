# Waste Alerts

Proactive alerts for expiring inventory items with matched recipe suggestions.

## Backlog

- MEP-009 Waste Reduction Alerts

## Route

`/waste-alerts`

## Components

- **WasteAlertsPageComponent** — Card grid showing expiring items with urgency-colored icons (red = expired/critical, amber = warning, blue = soon), expiry badges, recipe suggestion chips, and dismiss action per alert.

## Services Used

- `WasteAlertService` — Get alerts, dismiss alert
