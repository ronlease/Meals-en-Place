using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
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
    return 1;
}

if (options is null)
{
    Console.WriteLine(IngestOptions.UsageText);
    return 0;
}

if (!File.Exists(options.CsvPath))
{
    Console.Error.WriteLine($"ERROR: CSV file not found: {options.CsvPath}");
    return 1;
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
    return 2;
}

var services = new ServiceCollection();
services.AddDbContext<MealsEnPlaceDbContext>(opts => opts.UseNpgsql(connectionString));
services.AddScoped<IClaudeService, ClaudeService>();
services.AddScoped<IUomNormalizationService, UomNormalizationService>();

await using var serviceProvider = services.BuildServiceProvider();

// ── Ingest run ───────────────────────────────────────────────────────────
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

using (var scope = serviceProvider.CreateScope())
{
    var uomService = scope.ServiceProvider.GetRequiredService<IUomNormalizationService>();

    var progressInterval = 1000;
    var lastProgressAt = 0;

    foreach (var row in streamResult.Rows)
    {
        if (options.MaxRows is { } cap && summary.RecipesIngested >= cap)
        {
            summary.RecipesSkippedByMaxRows++;
            continue;
        }

        summary.RecipesIngested++;

        // Ingredients: container detection + deterministic UOM preview.
        foreach (var ingredient in row.Ingredients)
        {
            summary.TotalIngredientsProcessed++;

            var container = ContainerReferenceDetector.Detect(ingredient);
            if (container.IsContainerReference)
            {
                summary.ContainerFlaggedIngredients++;
                continue;
            }

            var deterministic = await uomService.TryResolveDeterministicallyAsync(ingredient);
            if (deterministic is not null)
            {
                summary.DeterministicallyResolvedIngredients++;
            }
            else
            {
                summary.UomDeferredToQueue++;
            }
        }

        // Directions: prose-filter retention counts.
        foreach (var step in row.Directions)
        {
            summary.RecordProseFilter(InstructionProseFilter.Evaluate(step));
        }

        // CanonicalIngredient upserts happen in the live path (Phase 4b).
        // For dry run, approximate the unique-token count by counting NER entries.
        if (options.DryRun)
        {
            summary.CanonicalIngredientsCreated += row.Ner.Count;
        }

        if (summary.RecipesIngested - lastProgressAt >= progressInterval)
        {
            Console.Write($"\rProcessed {summary.RecipesIngested:N0} recipes...");
            lastProgressAt = summary.RecipesIngested;
        }
    }
}

summary.StopTimer();

Console.WriteLine();
Console.WriteLine(summary.Format(options, streamResult.Counters));

if (options.DryRun)
{
    Console.WriteLine(
        "NOTE: dry-run output. CanonicalIngredientsCreated is an UPPER-BOUND estimate " +
        "(sum of NER entries across recipes, without deduplication).");
}

return 0;
