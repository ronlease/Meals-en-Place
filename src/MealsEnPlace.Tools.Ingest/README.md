# MealsEnPlace.Tools.Ingest

Offline console tool that imports recipes from the Kaggle "Recipe Dataset (over 2M)" CSV into the local Meals en Place database. Built for the MEP-026 bulk-ingest story.

## Usage

```bash
MealsEnPlace.Tools.Ingest --csv <path> [--dry-run] [--max-rows N]
```

### Arguments

- `--csv <path>` — Path to `recipes_data.csv` on the local machine. The dataset must be downloaded from Kaggle by the user under the dataset's CC BY-NC-SA 4.0 terms; it is never committed to this repository.
- `--dry-run` — Read and count rows but do not write to the database. Useful for validating a CSV and estimating a full ingest before committing to it.
- `--max-rows N` — Cap the number of ingested rows (excluding skipped Recipes1M rows). Useful for smoke-testing.

### Dataset acquisition

Each user obtains their own copy of the dataset from Kaggle:

> https://www.kaggle.com/datasets/wilmerarltstrmberg/recipe-dataset-over-2m

Download the archive, extract `recipes_data.csv`, and pass its path to the tool via `--csv`.

The tool automatically skips rows whose `source` column equals `Recipes1M`, per the MEP-025 decision to respect MIT's access restriction on the underlying Recipe1M+ subset. Expect ~26% of rows (≈588k of 2.23M) to be filtered out on this basis.

## Behavior

- **Streams the CSV** row-by-row using CsvHelper. The full 2.31 GB file is never loaded into memory.
- **Scrubs NUL bytes** from every string as it leaves the reader so Postgres text columns never see `\0` (MEP-026 hotfix; Kaggle rows deep in the dump carry embedded NULs).
- **Parses ingredient / directions / NER arrays** as JSON. Malformed rows are counted and skipped, not fatal.
- **Detects container references** (can, jar, box, etc.) via `ContainerReferenceDetector` and persists them as unresolved `RecipeIngredient` rows with the original text in `Notes`.
- **Resolves units of measure deterministically** through `InMemoryUnitOfMeasureResolver` — the tool preloads `UnitOfMeasure` and `UnitOfMeasureAlias` into memory so per-ingredient resolution is O(1). Unresolved tokens go to the `UnresolvedUnitOfMeasureToken` review queue instead of invoking Claude, preserving quota.
- **Normalizes NER tokens** via `NerTokenNormalizer` before any canonical lookup or creation. Rules (applied in order): trim; strip leading/trailing non-[letter/digit/apostrophe] characters; collapse internal whitespace; repeatedly strip trailing stopwords (a, an, the, and, or, of, for, with, to, add, plus); reject if empty; reject if no letter; reject if all words are stopwords. Tokens that differ from their original after normalization are counted as "NER tokens normalized"; tokens rejected by any rule are counted as "NER tokens rejected" and produce no `CanonicalIngredient` row.
- **Upserts canonical ingredients** from the NER column via `CanonicalIngredientRegistry`. Each unique NER token becomes a `CanonicalIngredient` row; raw ingredient strings link to the longest NER token they contain.
- **Truncates over-length strings** at the EF-configured column caps before writing (recipe title, source URL, ingredient notes, unresolved-token sample columns). Over-length source URLs are dropped to null rather than truncated, since a truncated URL is worse than none.
- **Applies `InstructionProseFilter`** to directions. Steps with first-person pronouns or >40 words after parenthetical stripping are dropped.
- **Batches writes** in groups of 100 recipes (`IngestConstants.RecipeBatchSize`) with explicit `ChangeTracker.Clear()` between flushes so the working set stays bounded across the full 1.6M+ row run.
- **Backfills `RecipeReferenceCount`** at end-of-run via a single `UPDATE … FROM (SELECT … GROUP BY …)` statement so the denormalized autocomplete ranking column reflects the full ingested dataset without per-row overhead during the batch loop.

## Resetting and re-ingesting

> **WARNING: this is a destructive, irreversible operation.** `docker compose down -v` deletes
> the `pgdata` volume and **all data** — inventory items, user-created ingredients, recipes,
> meal plans, and everything else. It cannot be undone.
>
> **Do not run the ingest tool twice against the same database without a reset first.** The tool
> does not detect previously imported recipes: running it twice duplicates every recipe in the
> catalog and doubles every ingredient reference count. Canonical ingredients themselves are
> deduplicated by name, so the duplicates are invisible until you notice every recipe twice.

Run the following commands from the repository root:

```bash
# 1. Drop and recreate the database
docker compose down -v && docker compose up -d

# 2. Apply migrations
dotnet ef database update --project src/MealsEnPlace.Api

# 3. Dry-run first to confirm the ingest looks correct
dotnet run --project src/MealsEnPlace.Tools.Ingest -- --csv src/MealsEnPlace.Tools.Ingest/data/recipes_data.csv --dry-run

# 4. Run the full ingest (takes ~40 minutes on a mid-range desktop)
dotnet run --project src/MealsEnPlace.Tools.Ingest -- --csv src/MealsEnPlace.Tools.Ingest/data/recipes_data.csv

# 5. Fold morphological duplicates (dry-run first, then apply).
#    The ingest creates one CanonicalIngredient per distinct NER token; the
#    dedup pass folds plural and prep-modifier variants into a single survivor
#    so "onion", "chopped onion", and "diced onions" map to one canonical (MEP-038).
#    Both tools recompute RecipeReferenceCount at the end so the autocomplete
#    search ranking is correct after each step.
dotnet run --project src/MealsEnPlace.Tools.Dedup -- --dry-run
dotnet run --project src/MealsEnPlace.Tools.Dedup
```

`dotnet ef database update` applies all migrations so seed data (units of measure, seasonality
windows, etc.) lands before the ingest begins. The ingest tool is only run **once** per clean
database.

## Performance

A full live ingest of the 2.23M-row CSV (filtering out the 588k Recipes1M rows) produces ~1.64M recipes, ~14M recipe ingredients, and ~146k canonical ingredients in roughly 40 minutes against local Postgres 16 on a mid-range desktop. The dominant cost is `SaveChangesAsync` batching; CSV parsing and in-memory resolution are not the bottleneck.

## Connection string

The tool reads `ConnectionStrings:DefaultConnection` from `appsettings.json`, with `ConnectionStrings__DefaultConnection` as the environment-variable override. The committed default targets the local Docker Compose Postgres on port 5433.

## Files

- `Program.cs` — orchestration entry point (top-level statements, batch loop, flush logic)
- `IngestOptions.cs` — CLI argument parsing
- `IngestConstants.cs` — batch size, progress interval, column caps, default FK values
- `KaggleRow.cs` / `KaggleRowReader.cs` — streaming CSV reader + row DTO (NUL scrubbing lives here)
- `IngestSummary.cs` — running counters and final summary formatter
- `CanonicalIngredientRegistry.cs` — in-memory dedup + upsert for `CanonicalIngredient` rows from NER tokens, plus longest-substring NER-to-ingredient linker
- `InMemoryUnitOfMeasureResolver.cs` — pre-loaded unit-of-measure / alias lookup with deferred-queue upsert on miss
- `IngestUnitOfMeasureResolution.cs` — result DTO for the resolver

`ContainerReferenceDetector` and `InstructionProseFilter` are reused from `MealsEnPlace.Api.Common` rather than duplicated here.
