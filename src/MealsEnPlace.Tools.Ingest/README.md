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
- **Parses ingredient / directions / NER arrays** as JSON. Malformed rows are counted and skipped, not fatal.
- **Detects container references** (can, jar, box, etc.) via `ContainerReferenceDetector`.
- **Previews unit of measure resolution** against the database's abbreviations, names, aliases, and count-noun fallback. Tokens with no deterministic match are counted toward the review-queue deferral total (Phase 4a: count only; Phase 4b: actually persist the queue row).
- **Applies `InstructionProseFilter`** to directions. Steps with first-person pronouns or >40 words after parenthetical stripping are dropped.

## Connection string

The tool reads `ConnectionStrings:DefaultConnection` from `appsettings.json`, with `ConnectionStrings__DefaultConnection` as the environment-variable override. The committed default targets the local Docker Compose Postgres on port 5433.

## Current status (Phase 4a)

Phase 4a is a **read-only dry-run skeleton**:

- CSV streaming with filter ✓
- Container detection count ✓
- Unit of measure deterministic-resolution preview (no writes) ✓
- Prose-filter retention count ✓
- Summary output with timing ✓

Phase 4b will add:

- Recipe / RecipeIngredient persistence
- CanonicalIngredient upserts from NER
- `NormalizeOrDeferAsync` writes to the `UnresolvedUnitOfMeasureToken` review queue
- Batched `SaveChangesAsync` flushing

Phase 4c will add:

- Synthetic CSV fixtures for unit testing
- Integration test covering the full path with an in-memory DB

## Files

- `Program.cs` — orchestration entry point
- `IngestOptions.cs` — CLI argument parsing
- `KaggleRow.cs` / `KaggleRowReader.cs` — streaming CSV reader + row DTO
- `IngestSummary.cs` — running counters and final summary formatter
