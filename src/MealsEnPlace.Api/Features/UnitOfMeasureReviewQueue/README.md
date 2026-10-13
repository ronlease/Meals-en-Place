# Unit of Measure Review Queue

Surfaces unresolved unit tokens from the MEP-026 bulk recipe ingest pipeline so the user can decide, once, how each token should map to a canonical `UnitOfMeasure`.

## What queues a token

When the ingest tool's `InMemoryUnitOfMeasureResolver` (or the service's `NormalizeOrDeferAsync`) is called and no deterministic step matches — neither direct abbreviation / name lookup, nor `UnitOfMeasureAlias`, nor the count-with-ingredient-noun fallback — the resolver writes or increments an `UnresolvedUnitOfMeasureToken` row rather than invoking Claude.

The token is the **leading word** of the text after the quantity (a leading parenthetical such as "(8 oz.)" is skipped and trailing `,;:` trimmed), not the whole remainder, so "2 sprigs fresh thyme" queues `sprigs`. The first two words and the first word are each tried against units and aliases before queueing. The count-with-ingredient-noun fallback (resolving to `ea`) applies only when the remainder is empty or begins with the ingredient noun (after skipping size descriptors such as "large" or "boneless"), e.g. "4 chicken breasts" for "chicken breast". Strings with no leading quantity ("a smidge") queue the whole string.

**Never queued (MEP-067):** junk words ("to", "or", "more", "None", "-"), range fragments (a mixed number or range such as "1-1/2" or "2-3" is parsed into the quantity), size descriptors (large, small, medium, ...; they are skipped before the unit word, so "2 large cloves garlic" resolves via `cloves`), to-taste lines ("salt", "salt and pepper", "salt to taste"), and container words (can, jar, pkg., package, ...; flagged by `ContainerReferenceDetector` before unit parsing). Resolution order: junk line, to-taste, butter-stick weight (113 g), abbreviation / name, alias, count fallback (ingredient noun or skipped size descriptor), defer.

`NormalizeAsync` (the non-ingest, real-time path) still falls back to Claude as before. Only the ingest path is queue-aware.

## Actions

- **`GET /api/v1/unit-of-measure-review-queue`** — list every queued token, ordered by `Count` descending then `LastSeenAt` descending.
- **`POST /api/v1/unit-of-measure-review-queue/{id}/map`** — map the token to a canonical `UnitOfMeasure`. Creates a `UnitOfMeasureAlias` row so future occurrences resolve deterministically, then deletes the queue row.
- **`POST /api/v1/unit-of-measure-review-queue/{id}/ignore`** — delete the queue row without creating an alias. Re-encountering the same token in a future ingest will re-queue it.

## Alias uniqueness and the `allowDuplicateAlias` override

Recipe notation uses case meaningfully (`T` = Tablespoon, `t` = Teaspoon, a 3× quantity difference), so `UnitOfMeasureAlias` has **no database-level unique index** on `Alias`. The `map` endpoint enforces uniqueness at the service layer: if an alias with the same text already exists, the request fails with **409 Conflict** unless the caller passes `allowDuplicateAlias: true` to force the insert. The override exists exclusively for legitimate case-sensitive variants.

## Files
- `UnitOfMeasureReviewQueueController.cs` — GET / POST map / POST ignore endpoints
- `UnresolvedUnitOfMeasureTokenResponse.cs` — list item DTO
- `MapTokenToUnitOfMeasureRequest.cs` / `MapTokenToUnitOfMeasureResponse.cs` — map action DTOs
