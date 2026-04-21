# MealsEnPlace.Tools.Dedup

Offline console tool that folds morphologically-equivalent `CanonicalIngredient` rows into a single survivor per fold group and reassigns every foreign key that pointed at a loser. Built for the MEP-038 dedup story.

## Why

The MEP-026 Kaggle ingest creates one `CanonicalIngredient` per unique NER token. A 1.64M-recipe run produced ~146k canonical rows — roughly 10× the MEP-025 projection of 5k-15k. The overage is morphological noise in the Kaggle NER column: `onion`, `chopped onion`, `diced onion`, `onions`, and `fresh onions` each become distinct canonicals. That breaks recipe matching — an inventory entry for "1 onion" won't match a recipe calling for "2 cups chopped onion" even though the user clearly has the ingredient.

This tool collapses those variants into a single survivor row per group so the matching pipeline sees a unified view of each ingredient.

## Usage

```bash
MealsEnPlace.Tools.Dedup [--dry-run]
```

### Arguments

- `--dry-run` — Compute and print the fold groups without writing. Use this first against your real data to sanity-check counts before applying.
- `--help`, `-h` — Print usage.

## Behavior

1. **Loads every `CanonicalIngredient`** into memory along with per-table foreign-key usage counts across `RecipeIngredient`, `InventoryItem`, `ShoppingListItem`, `SeasonalityWindow`, and `ConsumeAuditEntry`.
2. **Normalizes each name** through `CanonicalNameNormalizer`: lowercase, strip cosmetic prep modifiers (`chopped`, `diced`, `fresh`, etc.), collapse English plurals, sort the remaining tokens. Size-significant modifiers like `baby`, `mini`, and `jumbo` are *not* stopwords, so "baby carrot" stays distinct from "carrot".
3. **Groups by normalized key** via `FoldGroupResolver`. Multi-member groups become fold candidates. Single-member groups are skipped.
4. **Picks a survivor** per group: shortest name wins, ties broken by highest reference count, final tie broken alphabetically.
5. **Applies the fold** (unless `--dry-run`): for each loser row, insert a `CanonicalIngredientAlias` capturing the folded name, bulk-`UPDATE` every child-table FK to point at the survivor via `ExecuteUpdateAsync`, then `DELETE` the loser row.
6. **Batches work** in groups of `DedupConstants.FoldGroupBatchSize` per transaction so a failure mid-run doesn't leave the database half-applied.

Folds are non-destructive in the sense that the original canonical name is preserved in the `CanonicalIngredientAliases` table. A future story could offer a one-click "unfold" based on that row.

## Recommended workflow

```bash
# 1. Always dry-run first
MealsEnPlace.Tools.Dedup --dry-run

# 2. Review the reported group count and FK reassignment totals
#    against what you expected. Big surprises (say, a single group with
#    half your canonicals) usually mean the stopword list is too aggressive
#    for your data and should be tightened before applying.

# 3. Apply
MealsEnPlace.Tools.Dedup
```

## Connection string

The tool reads `ConnectionStrings:DefaultConnection` from `appsettings.json`, with `ConnectionStrings__DefaultConnection` as the environment-variable override. The committed default targets the local Docker Compose Postgres on port 5433.

## Files

- `Program.cs` — orchestration entry point (top-level statements)
- `DedupOptions.cs` — CLI argument parsing
- `DedupConstants.cs` — batch size, exit codes
- `DedupSummary.cs` — running counters and final summary formatter
- `CanonicalNameNormalizer.cs` — pure string normalizer producing fold-group keys
- `FoldGroupResolver.cs` — pure function from candidates to `FoldGroup`s with survivor selection
- `FoldGroup.cs` — DTOs for a fold group + its candidate
- `CanonicalIngredientDedupRunner.cs` — orchestrates the DB work (snapshot load, plan, apply)
