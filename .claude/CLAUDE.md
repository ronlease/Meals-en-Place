# Meals en Place — Claude Code Orchestration

## Project Overview
Meals en Place is a personal recipe and meal planning tool. It tracks pantry, fridge, and
freezer inventory; matches available ingredients against a local recipe library sourced from
open recipe APIs; generates meal plans that minimize waste; and identifies what produce is in
season. Single-user, local deployment only.

## Tech Stack
- **API:** ASP.NET Core 10 Web API, Entity Framework Core 10, PostgreSQL
- **Frontend:** Angular 21, standalone components, Angular Material, ApexCharts
- **Auth:** None — single user, local deployment
- **AI:** Claude API (recipe dietary classification, ingredient normalization, meal plan optimization, UOM resolution, container reference flagging)
- **External APIs:** TheMealDB (free, open recipe data — slated for removal under MEP-033), Open Food Facts (ingredient metadata)
- **Recipe catalog (bulk):** Kaggle "Recipe Dataset (over 2M)" ingested via `MealsEnPlace.Tools.Ingest`. Each user downloads their own copy under CC BY-NC-SA 4.0; the dataset is never committed. See [CITATION.cff](../CITATION.cff) and [README.md](../README.md) for setup.
- **Testing:** xUnit, Gherkin-style naming, FluentAssertions, Moq
- **Documentation:** Swashbuckle (OpenAPI/Swagger), PlantUML (C4 models)
- **Infrastructure:** Docker Compose (Windows and Fedora)
- **Secrets:** dotnet user-secrets (local)

## Repository Structure
```
MealsEnPlace/
  src/
    MealsEnPlace.Api/
      Common/                       # Shared types, UOM model, pagination, display conversion
      Features/
        Inventory/                  # Pantry, fridge, and freezer items
        Recipes/                    # Recipe library, import, dietary classification
        MealPlan/                   # Weekly meal plan generation and management
        Seasonality/                # Produce-in-season data and queries
        ShoppingList/               # Derived shopping list from meal plan gaps
        WasteReduction/             # Expiry tracking, use-it-up suggestions
      Infrastructure/
        Claude/                     # Claude API client and prompt management
        Data/                       # EF Core DbContext, migrations
        ExternalApis/               # TheMealDB client, Open Food Facts client
      Models/
        Entities/                   # EF Core entity classes
      Program.cs
    MealsEnPlace.Web/               # Angular 21 frontend
    MealsEnPlace.Tools.Ingest/      # Offline console tool for Kaggle bulk recipe ingest (MEP-026)
  tests/
    MealsEnPlace.Unit/              # xUnit unit tests, mirroring Features/ structure
    MealsEnPlace.Integration/       # xUnit integration tests (EF Core in-memory)
  docs/
    backlog.md                      # Owned by Product Owner agent
    c4/                             # PlantUML C4 model files
    spikes/                         # Spike research artifacts (e.g., MEP-025)
  docker-compose.yml
  CITATION.cff                      # Attribution for the bundled Kaggle dataset
  .claude/
    agents/
```

## Agent Roster
| Agent | File | Responsibility |
|---|---|---|
| Product Owner | `product-owner.md` | Backlog, business problems, acceptance criteria |
| Architect | `architect.md` | Swashbuckle OpenAPI, PlantUML C4 models |
| Backend Engineer | `backend-engineer.md` | .NET 10 API, recipe pipeline, Claude integration, external API clients |
| Frontend Engineer | `frontend-engineer.md` | Angular 21, pantry UI, meal plan board, recipe browser |
| QA Engineer | `qa-engineer.md` | Gherkin scenarios, xUnit tests |

## Workflow
- Workflow is fluid. Any agent may be invoked at any time.
- **The user must approve all file changes before they are written.**
- The Backend Engineer and QA Engineer work alongside each other:
  the Engineer implements a feature, QA immediately writes tests for it before moving on.
- The Architect generates and updates OpenAPI specs and C4 models after API changes.
- The Product Owner owns `docs/backlog.md` exclusively.

## Routing Rules
- "backlog", "story", "feature request", "business problem" → Product Owner
- "C4", "diagram", "architecture", "swagger", "openapi" → Architect
- "implement", "endpoint", "controller", "service", "repository", "EF", "migration", "recipe", "pantry", "inventory", "meal plan", "UOM", "seasonality", "shopping list", "container" → Backend Engineer
- "component", "angular", "frontend", "UI", "page", "route", "chart", "dashboard", "board" → Frontend Engineer
- "test", "gherkin", "scenario", "given/when/then", "coverage" → QA Engineer

## Conventions
- C# follows Microsoft conventions. Use `var` where type is obvious.
- All API endpoints are versioned under `/api/v1/`.
- All secrets go through `dotnet user-secrets` locally. Never hardcode credentials.
- EF Core migrations are explicit — never auto-migrate on startup.
- Angular uses standalone components. No NgModules.
- All new features require a backlog entry before implementation.
- **All fields, properties, methods, and variables within a class must be declared in alphabetical order.** Applies to both C# and TypeScript. Enforced to ease diffs and code review.
- **Avoid abbreviations in domain names.** Spell out terms like `UnitOfMeasure` (not `UOM` / `Uom`), `UnitOfMeasureAlias`, etc. Universal programming abbreviations (HTTP, JSON, SQL, CSV, API, UI, DB, DTO, EF, ID) are OK. Existing legacy `Uom...` naming predates this rule; do not undertake a repo-wide rename without explicit request.
- Commit locally freely as work progresses. Only push to origin or open/update PRs when explicitly asked.
- Never commit directly to main. Always create a feature branch and commit there.

## Domain Concepts
- **InventoryItem:** A food item the user currently has on hand, with location (Pantry/Fridge/Freezer), quantity, unit of measure, and optional expiry date. For items sold in containers (cans, jars, boxes, packets), the user declares the net weight or volume explicitly at entry time — the system never assumes a container size.
- **CanonicalIngredient:** A normalized ingredient entity that multiple inventory items and recipe ingredients map into (e.g., "Chicken Breast" regardless of brand or description).
- **UnitOfMeasure (UOM):** A canonical unit (cup, gram, oz, each, tbsp, etc.) with conversion factors between compatible units. The system resolves known units deterministically; Claude resolves colloquial or ambiguous units. Container references ("1 can", "1 jar", "1 box", "1 packet") are never a UOM — they are flagged for user declaration.
- **ContainerReference:** A string from a recipe ingredient or inventory entry that refers to a container size rather than a unit of measure (e.g., "1 can", "1 jar"). The system detects these on import, flags them, and requires the user to declare the net weight or volume before the ingredient participates in matching math. The original string is preserved in the Notes field.
- **DisplaySystem:** User preference controlling whether quantities render as Imperial or Metric in the UI. Default: Imperial. Storage and all computation use metric base units internally (ml, g, ea); conversion to display units runs at the API response layer via `UomDisplayConverter`. Metric display is a future preference toggle — the `DisplaySystem` preference field is stubbed in the schema now to avoid a migration later.
- **Recipe:** A dish with a title, source, ingredient list (each with quantity and UOM), instructions, cuisine, dietary tags, and season affinity.
- **RecipeIngredient:** A join between a Recipe and a CanonicalIngredient with quantity, UOM, and a Notes field. Notes preserves the original recipe text (e.g., "1 can") when a container reference has been resolved to a declared weight or volume.
- **DietaryTag:** A Claude-derived classification: Vegetarian, Vegan, Carnivore, LowCarb, GlutenFree, DairyFree. A recipe may carry multiple tags.
- **MealPlan:** A weekly plan assigning Recipes to days and meal slots (Breakfast/Lunch/Dinner/Snack).
- **MealPlanSlot:** A single assignment of a Recipe to a day/slot within a MealPlan.
- **ShoppingList:** Auto-generated list of ingredients required by the active MealPlan but absent or insufficient in current inventory.
- **SeasonalityWindow:** A produce item with its peak season date range, scoped to USDA Zone 7a (York, PA) by default.
- **WasteAlert:** A system-generated notice that an inventory item is approaching expiry and matches one or more available recipes.

## Container Reference Resolution Flow
Container references appear on both the inventory and recipe sides and must be resolved
before any quantity math runs.

**Inventory side:** User adds "1 can of diced tomatoes." The system detects "can" as a
ContainerReference. The add/edit dialog prompts: "What is the net weight or volume of this
container?" User enters 14.5 oz. The InventoryItem stores Quantity = 14.5, UomId = oz, and
preserves the original entry string in Notes.

**Recipe side:** TheMealDB returns "1 can chopped tomatoes." On import, the system detects
"can" as a ContainerReference. The import flow flags this RecipeIngredient as unresolved.
The user is prompted to declare the expected size before the recipe participates in matching.
Once declared, RecipeIngredient stores Quantity = 14.5, UomId = oz, Notes = "1 can chopped
tomatoes." The recipe is marked fully resolved and enters the matching pool.

**Unresolved recipes do not participate in recipe matching.** A recipe with one or more
unresolved ContainerReferences displays with an "Awaiting Resolution" badge and is excluded
from MatchScore computation until all references are resolved.

**Shrinkflation note:** Container sizes are always user-declared and never assumed from a
lookup table. This is intentional — product sizes change without notice and a stale lookup
table produces silently wrong math.

## Recipe Matching Pipeline
1. User requests "what can I make?" with optional filters (cuisine, dietary tag, meal slot)
2. Backend aggregates current inventory into a normalized ingredient set with quantities
3. UOM normalization runs: convert all inventory quantities to metric base units (ml, g, ea)
4. Each fully-resolved recipe is scored: (matched ingredients / total ingredients), with a bonus for expiry-imminent items
5. Claude reviews the top N NearMatch candidates for feasibility and suggests substitutions for gaps
6. Results return ranked: Full Match, Near Match (with substitution notes), Partial Match
7. User may select a recipe to add to the MealPlan

## Meal Plan Generation
- User specifies a date range (default: current week) and slot preferences
- System ranks recipes by: waste-reduction score, seasonal affinity, dietary filters, recent history (avoid repetition within 7 days)
- Claude is invoked once per plan generation to review the full candidate set and optimize for variety and waste reduction
- Generated plan is editable — user can swap individual slots

## Pre-PR Checklist
Before any PR is opened, verify the following:
1. All README files in the repo are up-to-date
2. Every vertical slice (feature folder) under `Features/` (API) and `features/` (Angular) has a README.md
3. All PlantUML C4 diagrams in `docs/c4/` are up-to-date
4. All Swagger/OpenAPI docs are up-to-date (new endpoints documented, descriptions accurate)
5. All projects build successfully (`dotnet build`, `ng build`)
6. All tests pass (`dotnet test`)
7. Code coverage is at least 90% (excluding EF migrations, generated code, property-only DTOs, and Program.cs)
8. Delete any leftover `coverage-*/` and `**/TestResults/` directories before committing
9. Update `docs/backlog.md` — mark completed items as `Done`, verify no stale statuses
10. Run `dotnet format` and fix any violations

## Post-MVP Feature Stubs
The following feature folders are pre-created but contain no implementation. Do not implement
them until explicitly instructed:
- `Features/Canning/` — preservation window detection, canning yield estimation, safety checklist by produce type
- `Features/StoreSales/` — local store circular integration (blocked pending legal/API review; see backlog)
- `Features/Coupons/` — coupon aggregation (blocked pending third-party API partnership; see backlog)
