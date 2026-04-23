using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Tools.Dedup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// ── CLI parsing ───────────────────────────────────────────────────────────
DedupOptions? options;
try
{
    options = DedupOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine(DedupOptions.UsageText);
    return DedupConstants.ExitCodeInvalidArguments;
}

if (options is null)
{
    Console.WriteLine(DedupOptions.UsageText);
    return DedupConstants.ExitCodeSuccess;
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
    return DedupConstants.ExitCodeFatalError;
}

var services = new ServiceCollection();
services.AddDbContext<MealsEnPlaceDbContext>(opts => opts.UseNpgsql(connectionString));
await using var serviceProvider = services.BuildServiceProvider();

// ── Dedup run ─────────────────────────────────────────────────────────────
Console.WriteLine(options.DryRun
    ? "Starting DRY RUN -- no database writes will occur."
    : "Starting LIVE dedup -- database writes enabled.");
Console.WriteLine();

using var scope = serviceProvider.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<MealsEnPlaceDbContext>();

var summary = new DedupSummary();
var runner = new CanonicalIngredientDedupRunner(CanonicalNameNormalizer.Default);

try
{
    await runner.RunAsync(dbContext, summary, options.DryRun);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: dedup failed -- {ex.Message}");
    Console.Error.WriteLine(ex);
    return DedupConstants.ExitCodeFatalError;
}
finally
{
    summary.StopTimer();
}

Console.WriteLine(summary.Format(options));

if (options.DryRun)
{
    Console.WriteLine("NOTE: dry-run output. No CanonicalIngredient, alias, or foreign-key rows were modified.");
}

return DedupConstants.ExitCodeSuccess;
