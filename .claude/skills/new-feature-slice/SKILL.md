---
name: new-feature-slice
description: Scaffold a new feature slice: Features/<Name> in the API (controller, service, repository, DTOs, README, tests) and features/<name> in Angular. Use when a backlog item introduces a new feature folder rather than extending an existing one.
---

# New feature slice

Requires a backlog item. Confirm the item id and the slice name (PascalCase for the API, kebab-case for Angular) first.

## API: `src/MealsEnPlace.Api/Features/<Name>/`

Copy the shape of `Features/WasteReduction` (service only) or `Features/Inventory` (with repository). Flat folder, no subfolders.

- `<Name>Controller.cs`: `[ApiController]`, `[Route("api/v1/<kebab-name>")]`, XML docs on every action, `ProblemDetails` errors. Apply `UnitOfMeasureDisplayConverter` here when returning quantities.
- `I<Name>Service.cs` and `<Name>Service.cs`: logic in base units.
- `I<Name>Repository.cs` and `<Name>Repository.cs`: only when the slice needs data access beyond the `DbContext`. Skip it otherwise.
- DTOs: one file per request and response, XML docs on each property.
- `README.md`: backlog ids, endpoint table, key concepts, files.
- Register services in `Program.cs` (alphabetical within the block).
- Entity and configuration in `Models/Entities` and `Infrastructure/Data/Configurations` if new. Then use the `ef-migration` skill.

## Tests: `tests/MealsEnPlace.Unit/Features/<Name>/`

- Gherkin scenarios as comments above each test class.
- `Method_Scenario_Result` names, Moq for dependencies, FluentAssertions.
- Integration test in `tests/MealsEnPlace.Integration/Api/` using `WebApplicationFactory<Program>` with explicit seed data.

## Angular: `src/MealsEnPlace.Web/src/app/features/<name>/`

Copy the shape of `features/waste-alerts`.

- `<name>-page.component.ts` and `.spec.ts`: standalone, `inject()`, signals, loading and error states.
- `<name>.service.ts`: typed `HttpClient` calls, no unit conversion.
- `<name>.routes.ts` and an entry in `app.routes.ts`.
- `README.md`.

## Finish

Run the build and both test suites. Add a nav entry if the slice has a page. Update C4 with `update-c4` when you add a component.
