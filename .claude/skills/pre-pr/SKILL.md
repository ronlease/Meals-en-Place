---
name: pre-pr
description: Judgment checks to run before the user opens a PR from a feature branch: README freshness, Swagger accuracy, backlog statuses, C4 currency. Use when work is ready for review or when asked to prepare, finalize or check a branch for a PR. Mechanical gates (build, tests, format, coverage) run in CI and hooks, not here.
---

# Pre-PR

Do not open or update the PR. The user does that. Push only if asked.

## Mechanical gates (run once, report results)

- `dotnet build MealsEnPlace.slnx`, `dotnet test MealsEnPlace.slnx`
- In `src/MealsEnPlace.Web`: `npm run test:coverage`, `npx ng lint`, `npx ng build --configuration production`
- Remove leftover `coverage-*/`, `**/TestResults/` and `src/MealsEnPlace.Web/coverage/` before committing.
- CI also runs `dotnet format --verify-no-changes`, `prettier --check` and `render-c4.sh --check`. Run them locally when the diff touches those files.

## Judgment checks

1. **READMEs**: every feature folder touched by the diff has a README listing its endpoints, key concepts and files, and each claim matches the code. Root `README.md` structure and feature list still match. A new slice under `Features/` or `features/` has a README.
2. **Swagger**: every new or changed endpoint has `<summary>`, parameter, response and status-code docs. Every new DTO property has an XML doc. Descriptions match behavior. Routes sit under `/api/v1/`.
3. **Backlog**: the item for this branch is `Done`, its file is in `docs/backlog/archive/`, and `docs/backlog/index.md` agrees. Scan `git log --oneline -20` for merged items whose status is stale and fix them.
4. **C4**: if the change adds a container, component, external system or relationship, `docs/c4/*.puml` reflects it. Use the `update-c4` skill.
5. **Domain doc**: if the change alters a rule in `docs/domain.md` (units, container flow, matching, Claude), update it in the same branch.
6. **Leftovers**: no TODO without a backlog item, no commented-out code, no debug output, no secrets.

## Output

A short list: gates passed or failed with the failing output, then each judgment check as pass or the fix applied.
