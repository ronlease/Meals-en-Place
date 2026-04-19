using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using MealsEnPlace.Tools.Ingest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// ── CLI parsing ───────────────────────────────────────────────────────────
IngestOptions? options;
try
{
    options = IngestOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine(IngestOptions.UsageText);
    return IngestConstants.ExitCodeInvalidArguments;
}

if (options is null)
{
    Console.WriteLine(IngestOptions.UsageText);
    return IngestConstants.ExitCodeSuccess;
}

if (!File.Exists(options.CsvPath))
{
    Console.Error.WriteLine($"ERROR: CSV file not found: {options.CsvPath}");
    return IngestConstants.ExitCodeInvalidArguments;
}

// ── DI + config ───────────────────────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "ERROR: ConnectionStrings:DefaultConnection is not configured. " +
        "Set it in appsettings.json or via the ConnectionStrings__DefaultConnection environment variable.");
    return IngestConstants.ExitCodeFatalError;
}

var services = new ServiceCollection();
services.AddDbContext<MealsEnPlaceDbContext>(opts => opts.UseNpgsql(connectionString));
await using var serviceProvider = services.BuildServiceProvider();

// ── Ingest run ────────────────────────────────────────────────────────────
Console.WriteLine(options.DryRun
    ? "Starting DRY RUN -- no database writes will occur."
    : "Starting LIVE ingest -- database writes enabled.");
Console.WriteLine($"Input: {options.CsvPath}");
if (options.MaxRows is { } max)
{
    Console.WriteLine($"Max rows cap: {max:N0}");
}
Console.WriteLine();

var summary = new IngestSummary();
var streamResult = KaggleRowReader.Stream(options.CsvPath);

using var scope = serviceProvider.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<MealsEnPlaceDbContext>();

// Ingest-mode EF tuning: we manage batching and change tracking manually,
// so let the context skip per-change detection overhead.
dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

var uomResolver = await InMemoryUomResolver.LoadAsync(dbContext);
var canonicalRegistry = await CanonicalIngredientRegistry.LoadAsync(dbContext);

var lastProgressAt = 0;
var batchRecipeCount = 0;

foreach (var row in streamResult.Rows)
{
    if (options.MaxRows is { } cap && summary.RecipesIngested >= cap)
    {
        // Cap reached -- stop iterating. The remaining eligible rows are
        // left uncounted; the summary's "Rows read" reflects only the
        // portion we actually consumed from the stream.
        summary.StreamTerminatedByMaxRowsCap = true;
        break;
    }

    // ── Canonical ingredients from NER ────────────────────────────────────
    // Upsert a canonical row per unique NER token. Order matters for later
    // "longest match wins" linkage.
    foreach (var nerToken in row.Ner)
    {
        _ = canonicalRegistry.GetOrCreate(nerToken);
    }

    // ── Build the Recipe entity ───────────────────────────────────────────
    var retainedSteps = new List<string>();
    foreach (var step in row.Directions)
    {
        var proseResult = InstructionProseFilter.Evaluate(step);
        summary.RecordProseFilter(proseResult);
        if (proseResult.Retained)
        {
            retainedSteps.Add(step);
        }
    }

    var recipe = new Recipe
    {
        CuisineType = IngestConstants.DefaultCuisineType,
        Id = Guid.NewGuid(),
        Instructions = string.Join(IngestConstants.InstructionStepSeparator, retainedSteps),
        ServingCount = IngestConstants.DefaultServingCount,
        SourceUrl = string.IsNullOrWhiteSpace(row.Link) ? null : row.Link,
        TheMealDbId = null,
        Title = row.Title
    };

    // ── RecipeIngredients per raw ingredient ──────────────────────────────
    foreach (var rawIngredient in row.Ingredients)
    {
        summary.TotalIngredientsProcessed++;

        var bestNer = CanonicalIngredientRegistry.PickBestNerMatch(rawIngredient, row.Ner);
        if (bestNer is null)
        {
            summary.IngredientsWithoutNerMatch++;
            // No canonical linkage, no RecipeIngredient. The recipe still
            // imports; the ingredient is simply unrepresented in the
            // structured ingredient list.
            continue;
        }

        var canonicalId = canonicalRegistry.GetOrCreate(bestNer);

        var container = ContainerReferenceDetector.Detect(rawIngredient);
        if (container.IsContainerReference)
        {
            summary.ContainerFlaggedIngredients++;
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = canonicalId,
                Id = Guid.NewGuid(),
                IsContainerResolved = false,
                Notes = rawIngredient,
                Quantity = 0m,
                RecipeId = recipe.Id,
                UomId = null
            });
            continue;
        }

        var resolution = uomResolver.NormalizeOrDefer(rawIngredient, bestNer);
        if (resolution.WasDeferred)
        {
            summary.UomDeferredToQueue++;
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = canonicalId,
                Id = Guid.NewGuid(),
                IsContainerResolved = false,
                Notes = rawIngredient,
                Quantity = resolution.Quantity,
                RecipeId = recipe.Id,
                UomId = null
            });
        }
        else
        {
            summary.DeterministicallyResolvedIngredients++;
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = canonicalId,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Notes = null,
                Quantity = resolution.Quantity,
                RecipeId = recipe.Id,
                UomId = resolution.UomId
            });
        }
    }

    if (!options.DryRun)
    {
        dbContext.Recipes.Add(recipe);
    }

    summary.RecipesIngested++;
    batchRecipeCount++;

    // ── Batch flush ───────────────────────────────────────────────────────
    if (batchRecipeCount >= IngestConstants.RecipeBatchSize)
    {
        await FlushBatchAsync(dbContext, uomResolver, summary, options.DryRun);
        batchRecipeCount = 0;
    }

    if (summary.RecipesIngested - lastProgressAt >= IngestConstants.ProgressLoggingIntervalRecipes)
    {
        Console.Write($"\rProcessed {summary.RecipesIngested:N0} recipes...");
        lastProgressAt = summary.RecipesIngested;
    }
}

// Flush any trailing partial batch.
if (batchRecipeCount > 0)
{
    await FlushBatchAsync(dbContext, uomResolver, summary, options.DryRun);
}

summary.CanonicalIngredientsCreated = canonicalRegistry.NewRowsCreated;
summary.StopTimer();

Console.WriteLine();
Console.WriteLine(summary.Format(options, streamResult.Counters));

if (options.DryRun)
{
    Console.WriteLine("NOTE: dry-run output. No Recipe, RecipeIngredient, or UnresolvedUomToken rows were persisted.");
}

return IngestConstants.ExitCodeSuccess;

// ── Local functions ─────────────────────────────────────────────────────────

static async Task FlushBatchAsync(
    MealsEnPlaceDbContext dbContext,
    InMemoryUomResolver resolver,
    IngestSummary summary,
    bool dryRun)
{
    if (!dryRun)
    {
        dbContext.ChangeTracker.DetectChanges();
        await dbContext.SaveChangesAsync();
    }

    // Clear the tracker regardless of dry-run so memory does not grow across
    // batches. In dry-run mode this simply drops the in-memory entity graph.
    dbContext.ChangeTracker.Clear();
    resolver.ResetPerBatchState();
    summary.BatchesFlushed++;
}
