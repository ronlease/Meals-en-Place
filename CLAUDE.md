# Meals en Place

Personal recipe and meal planning tool: inventory (pantry, fridge, freezer), recipe matching, waste-minimizing meal plans, seasonal produce. Single user, local deployment, no auth.

Domain rules (units, container resolution, matching, Claude): [docs/domain.md](docs/domain.md). Read it before touching quantities, matching or Claude code.

## Stack

- API: ASP.NET Core 10, EF Core 10, PostgreSQL 17 (`docker compose up -d`, port 5433)
- Web: Angular 22 standalone, Angular Material, Vitest
- Tools: `MealsEnPlace.Tools.Ingest` (offline Kaggle recipe ingest; the CSV is gitignored and never committed)
- External: Todoist API v1, Claude API (stubbed in `ClaudeService`; see domain.md). Open Food Facts has no client yet.
- Tests: xUnit, FluentAssertions, Moq, Gherkin-style names

## Commands

Run in bash (WSL on Windows, native on Fedora).

```
dotnet build MealsEnPlace.slnx
dotnet test MealsEnPlace.slnx
dotnet format MealsEnPlace.slnx [--verify-no-changes]   # CI runs the check form
cd src/MealsEnPlace.Web && npm test            # Vitest
cd src/MealsEnPlace.Web && npm run test:coverage
cd src/MealsEnPlace.Web && npx ng lint
cd src/MealsEnPlace.Web && npx prettier --write "src/**/*.{ts,html,scss}"   # CI runs --check
cd src/MealsEnPlace.Web && npx ng build --configuration production
./scripts/render-c4.sh [--check]               # needs Docker
./scripts/db-backup.sh                         # pg_dump via docker compose; run on request only
```

## Workflow

- Work in thin vertical slices. One backlog item = one slice: migration, API, Angular and tests together, by one implementer. If an item does not fit one slice, split it in `docs/backlog/index.md` first.
- Backlog: `docs/backlog/index.md` lists items (id, title, status, priority). Read an item's file under `items/` only when working it. Done items live in `archive/`. Every feature needs a backlog item before code. Mark it `Done` when the work lands.
- Use the `implementer` agent to build a slice and the `reviewer` agent to review the diff against acceptance criteria. Skills: `new-backlog-item`, `new-feature-slice`, `ef-migration`, `update-c4`, `pre-pr`.
- Backlog statuses: Backlog, Proposed, In Progress, Blocked, Done, Post-MVP (Backlog).
- Before committing, remove leftover `coverage-*/`, `**/TestResults/` and `src/MealsEnPlace.Web/coverage/`.
- Diagrams: `docs/c4/` holds `context`, `container`, `component-api` and `component-web` `.puml` files and their committed PNGs. The render script pulls a pinned PlantUML image and needs network access. Show only built external systems (Claude API, Todoist; add Open Food Facts when a client exists) and never `Canning`, `StoreSales` or `Coupons`.
- Branch off `main` for all work. Commit locally as you go. Push feature branches when asked. The user opens PRs.
- Hooks (see `.claude/settings.json`) format edited files, block commits and pushes on `main`, block force-push, protect generated paths and verify the build at stop.

## Conventions

- Class members are in alphabetical order in TypeScript only. ESLint (`member-ordering`) is set to warn; 71 existing violations await cleanup (MEP-071). C# has no ordering rule.
- No abbreviations in domain names (`UnitOfMeasure`, not `UOM`). HTTP, JSON, SQL, CSV, API, UI, DB, DTO, EF, ID are fine.
- Routes live under `/api/v1/`. Controllers carry XML docs for Swagger.
- Feature code is a flat folder per slice under `Features/` (API) and `features/` (Angular). Each has a README.md.
- Secrets use `dotnet user-secrets`. Never hardcode credentials.
- Migrations: scaffold explicitly with `dotnet ef migrations add`. The API applies pending migrations on startup in Development only. A migration that drops a table or column needs a `// DESTRUCTIVE-ACK: <reason>` line.
- Fix compiler and lint warnings when you see them.
- Coverage floor is 90% on both suites. Do not lower a gate.
- Do not implement `Features/Canning/`, `StoreSales/` or `Coupons/`. They are README-only stubs.
- Writing style for docs and comments: active voice, terse, no emojis.

## Invariants

- Container sizes are user-declared. Never infer them.
- Unresolved recipes never get a `MatchScore`.
- Never produce a silently wrong quantity. Return a not-possible result instead.
- Low-confidence Claude output never applies silently.
- Services work in base units. Display conversion happens only at the response layer.
