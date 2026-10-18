# API conventions

- Layout: `Common/` (shared helpers), `Features/<Slice>/` (controller, `I<X>Service`, service, repository, DTOs, README), `Infrastructure/{Claude,Data,ExternalApis}`, `Models/Entities`, `Migrations/` (top level, generated).
- No cross-feature dependencies. Shared code goes in `Common/`.
- Entities use `Guid` ids and Fluent API configuration in `Infrastructure/Data/Configurations`.
- Controllers: `[ApiController]`, `[Route("api/v1/...")]`, `ProblemDetails` for errors, XML docs on every endpoint and DTO property (Swagger reads them).
- Quantity-bearing responses go through `UnitOfMeasureDisplayConverter` in the controller. Services stay in base units.
- Container words are detected by `ContainerReferenceDetector` before any unit parsing. Do not add a size lookup.
- Claude calls go through `IClaudeService` behind `IClaudeAvailability`. Parse structured JSON, degrade on failure, never throw to the caller, never apply Low confidence silently.
- Migrations: `dotnet ef migrations add <Name> --project src/MealsEnPlace.Api` (the Api project is both data and startup project). Pending migrations apply on startup in Development only (`IsDevelopment` guard in `Program.cs`). See the `dotnet-angular:ef-migration` skill.
- Migration seed data, including `UnitOfMeasureAlias` rows and other reference data, goes in the migration.
- Scratch-clone check: stop the API, then `docker exec mealsenplace-db psql -U mealsenplace -d postgres -c "CREATE DATABASE mep_scratch TEMPLATE mealsenplace"`. Generate SQL with `dotnet ef migrations script <last-applied> <new> --project src/MealsEnPlace.Api --output <file>` and run it on `mep_scratch` with `-v ON_ERROR_STOP=1`. Set `Command Timeout=300` for heavy backfills: the 30 second Npgsql default is too short on the 14M-row `RecipeIngredients` table. Drop `mep_scratch` afterward.
- Before a risky migration, suggest `./scripts/db-backup.sh` to the user. Never run it automatically.
- Model slices to copy: `Features/WasteReduction` (service only) and `Features/Inventory` (with repository). A new slice adds `<Name>Controller` (route `api/v1/<kebab-name>`), `I<Name>Service`/`<Name>Service`, a repository only when it needs more than the `DbContext`, one file per DTO, and a README (backlog ids, endpoint table, key concepts, files). Register services in `Program.cs`, alphabetical within the block. Unit tests go in `tests/MealsEnPlace.Unit/Features/<Name>/`, integration tests in `tests/MealsEnPlace.Integration/Api/`.
- Tests mirror `Features/` in `tests/MealsEnPlace.Unit`. Name tests `Method_Scenario_Result`, with the Gherkin scenario as a comment above the class. Integration tests use `WebApplicationFactory<Program>` with a fresh factory per class and explicit seed data.
- Moq all external clients. No real network or Claude calls in tests.
