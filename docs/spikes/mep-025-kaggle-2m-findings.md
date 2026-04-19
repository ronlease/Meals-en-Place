# MEP-025 — Kaggle "Recipe Dataset (over 2M)" Findings

Direct-from-file analysis of the Kaggle dataset originally identified as a Food.com candidate. The download turns out to be a multi-site aggregate (effectively a reupload of the RecipeNLG 2020 dataset), which changes the candidate profile meaningfully.

**Source:** [wilmerarltstrmberg/recipe-dataset-over-2m on Kaggle](https://www.kaggle.com/datasets/wilmerarltstrmberg/recipe-dataset-over-2m)
**Analyzed file:** `archive.zip` → `recipes_data.csv` (dated 2023-06-27)

## Raw numbers

| Metric | Value |
|---|---|
| Compressed download | 666 MB (`archive.zip`) |
| Uncompressed CSV | 2.31 GB (`recipes_data.csv`) |
| Row count | 2,231,149 |
| Distinct source sites | 28 |
| Avg ingredients per recipe (sample of 200k) | 7.2 (median 7, max 155) |

## Schema (7 columns)

| Column | Type | Content |
|---|---|---|
| `title` | string | Recipe name |
| `ingredients` | stringified JSON array | Raw ingredient strings, one per element |
| `directions` | stringified JSON array | Instruction step strings, one per element |
| `link` | string | Source URL (relative or full) |
| `source` | string | `"Gathered"` or `"Recipes1M"` — provenance label (important, see below) |
| `NER` | stringified JSON array | **Pre-extracted canonical ingredient names** — a meaningful ingest-time win |
| `site` | string | Root domain of source site |

### Sample row (row 50)
```
title:  Chicken Ole
site:   www.cookbooks.com
source: Gathered
link:   www.cookbooks.com/Recipe-Details.aspx?id=445786

ingredients (8 raw):
  "4 chicken breasts, cooked"
  "1 can cream of chicken soup"
  "1 can cream of mushroom soup"
  "1 can green chili salsa sauce"
  "1 can green chilies"
  ...

NER (8 canonical):
  ['green chilies', 'cream of chicken soup', 'green chili salsa sauce',
   'chicken breasts', 'cream of mushroom soup', 'onion',
   'corn tortilla', 'milk']

directions (4 steps):
  "Dice chicken."
  "Mix all ingredients together."
  ...
```

## Provenance breakdown

The dataset is **not pure Food.com** — it's a 28-site aggregate, and 26% of it is Recipe1M+ under the hood:

| `source` value | Rows | Share |
|---|---|---|
| `Gathered` (scraped from 28 sites) | 1,643,098 | 73.6% |
| `Recipes1M` | 588,044 | 26.4% |

### Top sites in the "Gathered" slice
| Site | Rows |
|---|---|
| www.cookbooks.com | 896,341 |
| www.food.com | 499,616 |
| www.epicurious.com | 129,444 |
| tastykitchen.com | 78,768 |
| www.myrecipes.com | 64,895 |
| www.allrecipes.com | 61,398 |
| cookpad.com | 61,020 |
| cookeatshare.com | 59,307 |
| www.yummly.com | 51,963 |
| www.tasteofhome.com | 51,594 |
| www.foodnetwork.com | 49,443 |
| food52.com | 48,501 |
| www.kraftrecipes.com | 42,010 |
| ...18 more sites | 296,798 |

## Critical concern: Recipes1M subset

**26.4% of the dataset (588,044 rows) is labeled `source: "Recipes1M"`** — the same Recipe1M+ data MIT just restricted to universities.

This creates a gray area. MIT's terms restrict *their* distribution channel, but this is a reupload by a third party. Whether RecipeNLG's authors had permission to redistribute those rows is unclear; whether Kaggle's ToS overrides MIT's is also unclear. The cleanest move is to sidestep the question entirely:

**Recommendation:** filter `WHERE source != 'Recipes1M'` on ingest. That leaves 1,643,098 rows from sites whose redistribution terms are about the underlying site-owners, not MIT — which aligns with the stance established earlier in MEP-025 (respect the Recipe1M+ access restriction).

The remaining 1.64M is still ~2,700× the TheMealDB catalog (600 recipes).

## Ingest pipeline implications

**Wins over pure-raw datasets:**
- `NER` column delivers pre-extracted canonical ingredient names. Row 50's NER (`['green chilies', 'cream of chicken soup', 'chicken breasts', ...]`) maps more or less directly into `CanonicalIngredient`. Big savings on seed-data curation — potentially 80%+ of canonical ingredients are pre-identified.
- Directions tend toward short imperatives (`"Dice chicken."`, `"Mix all ingredients together."`). Prose-filter pass should retain most steps.

**Parsing work that's unavoidable:**
- `ingredients` column is raw strings. No quantity/unit extraction — the existing `UomNormalizationService` and `ContainerReferenceDetector` will run at full pressure. Expect a container-reference flag rate meaningfully higher than Recipe1M+'s layer2+ would have been.
- `ingredients` and `directions` are **stringified Python-style lists** (using single quotes, with CSV-escaped double quotes). Parse with `ast.literal_eval` semantics, not JSON. Existing import pipeline may need a tweak here.
- `source: "Recipes1M"` filter at ingest.

**Not provided:**
- No per-ingredient quantity/unit/weight
- No nutrition data
- No image data

**Staleness is a non-issue for this project's scope.** The snapshot is dated 2023-06-27, but core ingredients and cooking techniques don't go stale -- spaghetti is still spaghetti, chicken is still chicken. Novelty and trending recipes are covered by the existing manual-recipe-add path (MEP-018), not by refreshing the catalog.

## License

**CC BY-NC-SA 4.0** — https://creativecommons.org/licenses/by-nc-sa/4.0/

Implications for this project:

- **Attribution (BY)** — credit the Kaggle uploader and underlying sites. A `CITATION.cff` at repo root handles this cleanly when implementation lands.
- **NonCommercial (NC)** — personal single-user use fits; any future commercialization of the tool breaks the terms and would require a different data source.
- **ShareAlike (SA)** — any *redistributed* adaptation of the dataset must be released under CC BY-NC-SA 4.0. The existing "no source data committed to the repo" AC already sidesteps this: if the data never leaves the local machine, SA doesn't trigger. Extracted facts (e.g. a `CanonicalIngredient` list of `["milk", "butter", ...]`) are not copyrightable and are not "adaptations" subject to SA.

Bottom line: CC BY-NC-SA 4.0 is compatible with the stance established earlier in MEP-025. Proceed.

## Open questions

1. **Redistribution posture.** Storing 1.64M recipes scraped from 28 commercial sites in a local DB is the same "scraped-from-copyrighted-sources" shape the user already decided is workable for personal use under 17 USC §102(b). The expanded site list is consistent with that reasoning — NYT Cooking, Bon Appétit, Food Network, etc. are represented, but for local single-user non-commercial use, nothing changes.
2. **Character encoding.** Sample shows `\u00b0` escape sequences for degree symbol — Unicode is present but encoded-in-text in some rows. Import needs to handle unescape.
3. **Duplicate detection across sites.** The same recipe may exist on multiple sites (e.g., a Food Network recipe mirrored on Yummly). Worth measuring before ingest to avoid double-counting.

## Related

- Backlog: [MEP-025](../backlog.md)
- Sister research doc: [mep-025-recipe1m-references.md](./mep-025-recipe1m-references.md)
