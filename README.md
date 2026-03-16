# Meals en Place

A personal recipe and meal planning tool that tracks pantry, fridge, and freezer inventory; matches available ingredients against a local recipe library; generates meal plans that minimize waste; and identifies what produce is in season. Single-user, local deployment.

## Tech Stack

- **API:** ASP.NET Core 10, Entity Framework Core 10, PostgreSQL
- **Frontend:** Angular 21, Angular Material
- **AI:** Claude API (dietary classification, ingredient normalization, meal plan optimization)
- **External APIs:** TheMealDB (recipe data), Open Food Facts (ingredient metadata)
- **Testing:** xUnit, FluentAssertions, Moq

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Setup

```bash
# Start PostgreSQL
docker compose up -d

# Apply database migrations
dotnet ef database update --project src/MealsEnPlace.Api

# Start the API (HTTPS on port 7274)
dotnet run --project src/MealsEnPlace.Api --launch-profile https

# In a separate terminal, start the frontend (HTTPS on port 4280)
cd src/MealsEnPlace.Web
npm install
ng serve
```

Open `https://localhost:4280` in your browser.

Swagger docs are available at `https://localhost:7274/swagger`.

### Running Tests

```bash
dotnet test
```

## Project Structure

```
src/
  MealsEnPlace.Api/          # ASP.NET Core Web API
    Common/                   # Shared types, UOM conversion/normalization, container detection
    Features/
      Inventory/              # Inventory CRUD, reference data endpoints
      Recipes/                # Recipe import, matching, container resolution
    Infrastructure/
      Claude/                 # Claude API client (stub) and prompt types
      Data/                   # EF Core DbContext, migrations, configurations
      ExternalApis/           # TheMealDB and Open Food Facts clients
    Models/Entities/          # Domain entities and enums
  MealsEnPlace.Web/           # Angular 21 frontend
    src/app/features/
      inventory/              # Pantry/Fridge/Freezer management
      recipes/                # Recipe browser, import, matching
      expiration/             # Upcoming expiration dates view
    src/app/core/             # Shared services and models
tests/
  MealsEnPlace.Unit/          # Unit tests (170 tests)
  MealsEnPlace.Integration/   # Integration tests (18 tests, WebApplicationFactory)
docs/
  backlog.md                  # Product backlog (MEP-001 through MEP-018)
  c4/                         # PlantUML C4 architecture diagrams
```

## Implemented Features

- **Inventory Management** (MEP-001) -- Track items across Pantry, Fridge, and Freezer with quantity, UOM, and expiry dates
- **UOM Normalization** (MEP-002) -- Convert between units; Claude resolves colloquial measures
- **Container Reference Resolution** (MEP-003) -- Detect "1 can", "1 jar" and prompt for actual weight/volume
- **Recipe Import** (MEP-004) -- Import from TheMealDB by name, cuisine, or category
- **Recipe Matching** (MEP-006) -- "What can I make?" ranked by ingredient coverage with waste bonus
- **Upcoming Expiration Dates** (MEP-015) -- Dashboard view of items approaching expiry
- **Material Icons** (MEP-016) -- Edit and delete icons in inventory table
- **Dark Mode** (MEP-017) -- Theme toggle with OS preference detection and localStorage persistence

## MVP Roadmap (Remaining)

- **Dietary Classification** (MEP-005) -- Claude auto-tags recipes
- **Meal Plan Generation** (MEP-007) -- Weekly plans optimized for waste reduction
- **Shopping List** (MEP-008) -- Auto-generated from meal plan gaps
- **Waste Alerts** (MEP-009) -- Notify when expiring items match available recipes
- **Seasonal Produce** (MEP-010) -- USDA Zone 7a growing season guidance

## License

See [LICENSE](LICENSE) for details.
