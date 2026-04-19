# UOM Review Queue

Surfaces unresolved unit tokens from the MEP-026 bulk recipe ingest pipeline so the user can decide, once, how each token should map to a canonical `UnitOfMeasure`.

## What queues a token

When `UomNormalizationService.NormalizeOrDeferAsync` is called (used by the ingest tool, not runtime endpoints) and no deterministic step matches -- neither direct abbreviation / name lookup, nor `UnitOfMeasureAlias`, nor the count-with-ingredient-noun fallback -- the service writes or increments an `UnresolvedUomToken` row rather than invoking Claude.

`NormalizeAsync` (the non-ingest, real-time path) still falls back to Claude as before. Only the ingest path is queue-aware.

## Actions

- **`GET /api/v1/uom-review-queue`** — list every queued token, ordered by `Count` descending then `LastSeenAt` descending.
- **`POST /api/v1/uom-review-queue/{id}/map`** — map the token to a canonical UOM. Creates a `UnitOfMeasureAlias` row so future occurrences resolve deterministically, then deletes the queue row.
- **`POST /api/v1/uom-review-queue/{id}/ignore`** — delete the queue row without creating an alias. Re-encountering the same token in a future ingest will re-queue it.

## Alias uniqueness and the `allowDuplicateAlias` override

Recipe notation uses case meaningfully (`T` = Tablespoon, `t` = Teaspoon, a 3× quantity difference), so `UnitOfMeasureAlias` has **no database-level unique index** on `Alias`. The `map` endpoint enforces uniqueness at the service layer: if an alias with the same text already exists, the request fails with **409 Conflict** unless the caller passes `allowDuplicateAlias: true` to force the insert. The override exists exclusively for legitimate case-sensitive variants.

## Files
- `UomReviewQueueController.cs` — GET / POST map / POST ignore endpoints
- `UnresolvedUomTokenResponse.cs` — list item DTO
- `MapTokenToUomRequest.cs` / `MapTokenToUomResponse.cs` — map action DTOs
