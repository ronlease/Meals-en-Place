# API conventions

- Layout: `Common/` (shared helpers), `Features/<Slice>/` (controller, `I<X>Service`, service, repository, DTOs, README), `Infrastructure/{Claude,Data,ExternalApis}`, `Models/Entities`, `Migrations/` (top level, generated).
- No cross-feature dependencies. Shared code goes in `Common/`.
- Entities use `Guid` ids and Fluent API configuration in `Infrastructure/Data/Configurations`.
- Controllers: `[ApiController]`, `[Route("api/v1/...")]`, `ProblemDetails` for errors, XML docs on every endpoint and DTO property (Swagger reads them).
- Quantity-bearing responses go through `UnitOfMeasureDisplayConverter` in the controller. Services stay in base units.
- Container words are detected by `ContainerReferenceDetector` before any unit parsing. Do not add a size lookup.
- Claude calls go through `IClaudeService` behind `IClaudeAvailability`. Parse structured JSON, degrade on failure, never throw to the caller, never apply Low confidence silently.
- Migrations: `dotnet ef migrations add <Name> --project src/MealsEnPlace.Api`. Pending migrations apply on startup in Development only. Test risky migrations against a scratch clone of the DB. See the `ef-migration` skill.
- Tests mirror `Features/` in `tests/MealsEnPlace.Unit`. Name tests `Method_Scenario_Result`, with the Gherkin scenario as a comment above the class. Integration tests use `WebApplicationFactory<Program>` with a fresh factory per class and explicit seed data.
- Moq all external clients. No real network or Claude calls in tests.
