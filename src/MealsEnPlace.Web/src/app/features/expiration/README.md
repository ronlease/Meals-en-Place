# Expiration

Displays inventory items approaching expiry with time-window filtering and color-coded urgency badges.

## Backlog

- MEP-015 Upcoming Expiration Dates

## Route

`/expiration`

## Components

- **ExpirationPageComponent** — Table of expiring items with button-toggle filter (All / 7 days / 3 days). Color-coded badges: green (>7 days), amber (3-7 days), red (<3 days).

## Services Used

- `InventoryService` — Loads items from all three locations via `forkJoin`
