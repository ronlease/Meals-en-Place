# MEP-025 — Recommendation

## TL;DR

**Adopt the Kaggle "Recipe Dataset (over 2M)" as the recipe catalog source.** Filter `source != 'Recipes1M'` on ingest to get ~1.64M usable recipes under the CC BY-NC-SA 4.0 license. Open a follow-up implementation ticket (MEP-026) to handle the parser gaps surfaced by the spike and to build a user-in-the-loop review queue that both resolves ambiguous units during ingest and teaches the system long-term via an alias table.

## Chosen source

[wilmerarltstrmberg/recipe-dataset-over-2m on Kaggle](https://www.kaggle.com/datasets/wilmerarltstrmberg/recipe-dataset-over-2m)

- 2,231,149 rows total; **1,643,098 usable after filtering out the `Recipes1M` rows** (the MIT-gated Recipe1M+ data this project already decided to stay clear of)
- 28 source sites, led by cookbooks.com (896k), food.com (500k), epicurious, allrecipes, foodnetwork, etc.
- Pre-extracted `NER` column provides canonical ingredient names per recipe — maps directly to `CanonicalIngredient` seed data
- License: **CC BY-NC-SA 4.0** — compatible with personal single-user non-commercial use, provided no source data is committed to the repo

## Why this, and not the others

| Candidate | Status | Why rejected |
|---|---|---|
| Recipe1M+ (MIT) | Rejected | Access restricted to universities and public institutions; this project has no institutional affiliation |
| RecipeNLG (direct) | Rejected | Upstream dependency on Recipe1M+; 588k rows of the chosen Kaggle dataset are labeled `source=Recipes1M` anyway — filter them out |
| Spoonacular / Edamam (live APIs) | Out of MVP scope | Per-query quota burn is architecturally incompatible with a single-user local deployment; revisit only if static path fails |
| Food.com-only Kaggle set (shuyangli94) | Deprioritized | ~180k recipes, smaller and single-sourced; chosen dataset supersets it with ~500k Food.com rows plus 1.1M+ from 27 other sites |

## Pipeline gaps the spike surfaced

Running the first 500 non-Recipes1M rows through a Python mirror of the existing parsers yielded:

- **Container-reference flag rate 10.5% of ingredients / 45.4% of recipes** — in line with expectations. No pipeline change needed; the existing MEP-003 resolution flow handles these.
- **Deterministic UOM resolve rate 0.4%** with the current parser. Root causes identified:
  1. Dotted abbreviations (`c.`, `tsp.`, `oz.`, `Tbsp.`, `lb.`) don't match the seeded UOM rows (`cup`, `tsp`, `oz`, `tbsp`, `lb`).
  2. Count-with-ingredient-noun patterns (`"4 boned chicken breasts"`) parse a number but no recognized unit.
- **Prose filter retention mean 77.9% / median 83.3%, 41.4% of recipes below 80%.** Filter as prototyped is too aggressive — drops legitimate imperatives that start with a preposition (`"In a bowl, combine..."`) or subordinator (`"When mixture bubbles..."`).
- **Storage projection ~1.2–2.2 GB Postgres** for 1.64M recipes. Non-issue.

Full measurement numbers and methodology in [mep-025-kaggle-2m-findings.md](./mep-025-kaggle-2m-findings.md).

## Proposed MEP-026 scope

An implementation ticket titled **"Recipe1M-family bulk ingest and UOM alias table"**. Components:

### 1. UOM alias table with human-in-the-loop review

Introduce a `UnitOfMeasureAlias` entity with a many-to-one relationship to `UnitOfMeasure`. Seed with common variants observed in the spike:

| Alias | Maps to |
|---|---|
| `c`, `c.` | cup |
| `t`, `t.` | teaspoon |
| `T`, `T.`, `Tbs`, `Tbsp.`, `Tbl` | tablespoon |
| `oz.`, `ozs`, `ozs.` | ounce |
| `lb.`, `lbs`, `lbs.` | pound |
| `fl. oz`, `fluid oz`, `fl. ozs` | fluid ounce |
| `tsp.` | teaspoon |
| `ml.`, `mls` | milliliter |
| `g.`, `gm`, `gms` | gram |
| `kg.`, `kgs` | kilogram |
| `pt.`, `pts` | pint |
| `qt.`, `qts` | quart |

Extend the UOM lookup order in `UomNormalizationService` to: **abbreviation → name → alias → review queue**.

When the parser can't resolve a token via any of the three lookups, write an `UnresolvedUomToken` row and halt Claude invocation for that ingredient. Present a UI (or CLI tool during bulk ingest) where the user decides:

- **Map to existing UOM** — creates an alias row, all future occurrences resolve deterministically
- **Defer to Claude** — one-shot resolution for truly ambiguous entries (`"a knob of butter"`)
- **Ignore** — mark the ingredient as unparseable; the recipe may be excluded from matching

This mirrors the MEP-003 container-resolution pattern and keeps the user in control. Side benefit: drastically reduces Claude invocations at ingest time (and cost), since a handful of user decisions cover thousands of recurring tokens.

### 2. Count-with-ingredient-noun handling

When `ParseMeasureString` returns a positive quantity but the remainder's leading tokens don't match any UOM / alias, check whether the remainder parses as an ingredient noun (e.g., "chicken breasts", "tomatoes", "eggs"). If yes, default to `ea` (each). If no, route to the review queue above.

### 3. Relaxed prose filter

Drop the first-word-must-be-imperative check. Keep the first-person-pronoun filter and the 40-word cap. Re-measure retention — expect mean >90%.

### 4. NER-driven `CanonicalIngredient` seeding

Use the dataset's `NER` column to seed `CanonicalIngredient` rows in bulk (deduplicated, normalized to lowercase). Projections suggest 5,000–15,000 rows — large enough to cover nearly all recipes but small enough for lightweight curation.

### 5. Offline admin-tool ingest

Build ingest as a standalone console tool (`MealsEnPlace.Tools.Ingest`) that the user runs once on their local machine after downloading the dataset themselves from Kaggle. Not a runtime endpoint. Not committed with data. The tool:

- Accepts a path to the local `recipes_data.csv`
- Filters `source != 'Recipes1M'`
- Applies the filters and parser logic above
- Writes `Recipe`, `RecipeIngredient`, `CanonicalIngredient`, and `UnresolvedUomToken` rows
- Reports a summary (N recipes ingested, M ingredients flagged for container resolution, K unresolved UOM tokens awaiting review)

### 6. Documentation

Update the README setup section to point at the Kaggle dataset page. Do **not** link the raw download URL — the user follows their own Kaggle account flow. Add a `CITATION.cff` noting the dataset source.

## Constraints to honor (from MEP-025 ACs)

- Source data is **not committed** to the repo — no CSV, no SQL dumps, no fixtures of real recipe text
- Only non-copyrightable derived data (canonical ingredient names, UOM mappings, aliases) may be seeded in migrations
- Setup docs link to the upstream Kaggle dataset page, not to any gated or archived copy
- Non-commercial single-user use is the only supported deployment posture; any future commercialization would require re-licensing the data

## Out of scope

- Nutrition data (not in the dataset; could be a separate future enrichment via Open Food Facts, which is already wired)
- Image ingestion (not in the dataset; and images inherit the site-owners' copyrights)
- Recipe freshness / incremental updates (2023 snapshot is fine per MEP-025 scope)
- Live APIs (Spoonacular, Edamam) — deferred out of MVP

## Acceptance for closing MEP-025

This recommendation plus the findings and references docs **is** the spike's output. MEP-025 can be marked Done on merge of this branch. Implementation proceeds under MEP-026 when prioritized.
