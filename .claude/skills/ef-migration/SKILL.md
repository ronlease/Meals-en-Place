---
name: ef-migration
description: Create, check and apply an EF Core migration for this repo. Use when an entity, configuration or seed change needs a schema change. Covers scaffolding, destructive-change acknowledgement, scratch-database verification and Development auto-apply.
---

# EF migration

## Policy

- Scaffold explicitly: `dotnet ef migrations add <Name> --project src/MealsEnPlace.Api`. Never edit a generated `*.Designer.cs` or the model snapshot by hand.
- The API applies pending migrations on startup in Development only (`IsDevelopment` guard in `Program.cs`). Production and test hosts do not auto-apply.
- Name the migration for the change (`AddRecipesTitleIndex`). One concern per migration.

## Steps

1. Change the entity and its `IEntityTypeConfiguration<T>` in `Infrastructure/Data/Configurations`.
2. Scaffold the migration. Read the generated `Up` and `Down`.
3. If `Up` contains `DropTable` or `DropColumn`, add a line `// DESTRUCTIVE-ACK: <reason>` to the migration file. The `guard-destructive-migration` hook blocks the file without it. State the data loss to the user.
4. Seed data goes in the migration, including `UnitOfMeasureAlias` rows and other reference data.
5. Verify on a scratch clone before the API ever runs against the live database, because startup applies the migration in Development:
   - Stop the API (open connections block the clone), then `docker exec mealsenplace-db psql -U mealsenplace -d postgres -c "CREATE DATABASE mep_scratch TEMPLATE mealsenplace"`.
   - `dotnet ef migrations script <last-applied> <new> --project src/MealsEnPlace.Api --output <scratch>/m.sql`, then run it against `mep_scratch` with `-v ON_ERROR_STOP=1`.
   - For heavy backfills set `Command Timeout=300` in the connection string. The Npgsql default of 30 seconds is too short on the 14M-row `RecipeIngredients` table.
   - `EXPLAIN ANALYZE` any hot query, then `DROP DATABASE mep_scratch`.
6. Before a risky migration, suggest `./scripts/db-backup.sh` to the user. Never run it automatically.
7. Run `dotnet test`. Update the owning feature README if it documents the schema.
