// Feature: CanonicalIngredientDedupRunner folds groups end-to-end against an EF Core context
//
// Scenario: Dry-run populates the summary without writing
//   Given three CanonicalIngredients that normalize to one key plus RecipeIngredient FKs on each
//   When RunAsync is invoked with dryRun=true
//   Then the DB rows are unchanged
//   And the summary reports the projected fold group and FK counts
//
// Scenario: Live run folds losers into the survivor and reassigns every FK table
//   Given losers that have FKs in RecipeIngredient / InventoryItem / ShoppingListItem / SeasonalityWindow / ConsumeAuditEntry
//   When RunAsync is invoked with dryRun=false
//   Then only the survivor CanonicalIngredient remains
//   And every child-table row now points at the survivor
//   And an alias row exists for each loser, pointing at the survivor
//
// Scenario: Empty database is a no-op
//   Given no CanonicalIngredient rows
//   Then the summary reports zero fold groups and no writes happen

using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using MealsEnPlace.Tools.Dedup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Tools.Dedup;

public sealed class CanonicalIngredientDedupRunnerTests : IDisposable
{
    // SQLite in-memory (not EF InMemory): the runner uses ExecuteUpdateAsync +
    // explicit transactions, neither of which the EF InMemory provider
    // supports. SQLite is a real relational engine that honors both, and
    // its :memory: mode keeps each test isolated without disk I/O.
    //
    // DedupTestDbContext drops UserPreferences from the model because its
    // Postgres-flavored CHECK constraint ("Id" = '...') doesn't round-trip
    // through SQLite's parser. The dedup pipeline never touches that table
    // so the omission is safe for this test fixture.
    private readonly SqliteConnection _connection;
    private readonly DedupTestDbContext _dbContext;
    private readonly CanonicalIngredientDedupRunner _runner;

    public CanonicalIngredientDedupRunnerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new DedupTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _runner = new CanonicalIngredientDedupRunner(CanonicalNameNormalizer.Default);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private sealed class DedupTestDbContext : MealsEnPlaceDbContext
    {
        public DedupTestDbContext(DbContextOptions<MealsEnPlaceDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<UserPreferences>();
        }
    }

    [Fact]
    public async Task RunAsync_NoFoldCandidates_IsNoOp()
    {
        // The CanonicalIngredients table ships with 10 seeded seasonality
        // rows (tomatoes, corn, zucchini, ...). None of them normalize to
        // a shared key, so the runner should load them and find zero folds.
        var summary = new DedupSummary();

        await _runner.RunAsync(_dbContext, summary, dryRun: false);

        summary.CanonicalIngredientsLoaded.Should().Be(SeededCanonicalCount);
        summary.FoldGroupCount.Should().Be(0);
        summary.LoserRowsDeleted.Should().Be(0);
        summary.AliasRowsWritten.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_DryRun_ReportsPlanWithoutWriting()
    {
        var recipe = SeedRecipe();
        var survivor = SeedCanonicalIngredient("onion");
        var loser1 = SeedCanonicalIngredient("chopped onion");
        var loser2 = SeedCanonicalIngredient("fresh onions");
        SeedRecipeIngredient(loser1.Id, recipe.Id);
        SeedRecipeIngredient(loser1.Id, recipe.Id);
        SeedRecipeIngredient(loser2.Id, recipe.Id);
        await _dbContext.SaveChangesAsync();

        var summary = new DedupSummary();

        await _runner.RunAsync(_dbContext, summary, dryRun: true);

        summary.CanonicalIngredientsLoaded.Should().Be(SeededCanonicalCount + 3);
        summary.FoldGroupCount.Should().Be(1);
        summary.LoserRowsDeleted.Should().Be(2);
        summary.AliasRowsWritten.Should().Be(2);
        summary.RecipeIngredientFksReassigned.Should().Be(3);

        (await _dbContext.CanonicalIngredients.CountAsync()).Should().Be(SeededCanonicalCount + 3);
        (await _dbContext.CanonicalIngredientAliases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_Live_FoldsLosersAndReassignsEveryChildTable()
    {
        var recipe = SeedRecipe();
        var mealPlan = SeedMealPlan();
        var mealPlanSlot = SeedMealPlanSlot(mealPlan.Id, recipe.Id);

        var survivor = SeedCanonicalIngredient("carrot");
        var loser = SeedCanonicalIngredient("chopped carrots");

        var recipeIngredient = SeedRecipeIngredient(loser.Id, recipe.Id);
        var inventoryItem = SeedInventoryItem(loser.Id);
        var shoppingListItem = SeedShoppingListItem(loser.Id);
        var seasonalityWindow = SeedSeasonalityWindow(loser.Id);
        var consumeAuditEntry = SeedConsumeAuditEntry(loser.Id, mealPlanSlot.Id);
        await _dbContext.SaveChangesAsync();

        var summary = new DedupSummary();

        await _runner.RunAsync(_dbContext, summary, dryRun: false);

        summary.FoldGroupCount.Should().Be(1);
        summary.LoserRowsDeleted.Should().Be(1);
        summary.RecipeIngredientFksReassigned.Should().Be(1);
        summary.InventoryItemFksReassigned.Should().Be(1);
        summary.ShoppingListItemFksReassigned.Should().Be(1);
        summary.SeasonalityWindowFksReassigned.Should().Be(1);
        summary.ConsumeAuditEntryFksReassigned.Should().Be(1);

        // 10 seeded + 1 survivor = 11; the one loser is gone.
        // ExecuteUpdate / ExecuteDelete bypass the EF change tracker so assert
        // against AsNoTracking reads to see the actual DB state.
        (await _dbContext.CanonicalIngredients.AsNoTracking().CountAsync()).Should().Be(SeededCanonicalCount + 1);
        (await _dbContext.CanonicalIngredients.AsNoTracking()
            .AnyAsync(c => c.Id == survivor.Id)).Should().BeTrue();
        (await _dbContext.CanonicalIngredients.AsNoTracking()
            .AnyAsync(c => c.Id == loser.Id)).Should().BeFalse();

        var alias = await _dbContext.CanonicalIngredientAliases.AsNoTracking().SingleAsync();
        alias.Alias.Should().Be("chopped carrots");
        alias.CanonicalIngredientId.Should().Be(survivor.Id);

        (await _dbContext.RecipeIngredients.AsNoTracking()
            .SingleAsync(r => r.Id == recipeIngredient.Id)).CanonicalIngredientId.Should().Be(survivor.Id);
        (await _dbContext.InventoryItems.AsNoTracking()
            .SingleAsync(r => r.Id == inventoryItem.Id)).CanonicalIngredientId.Should().Be(survivor.Id);
        (await _dbContext.ShoppingListItems.AsNoTracking()
            .SingleAsync(r => r.Id == shoppingListItem.Id)).CanonicalIngredientId.Should().Be(survivor.Id);
        (await _dbContext.SeasonalityWindows.AsNoTracking()
            .SingleAsync(r => r.Id == seasonalityWindow.Id)).CanonicalIngredientId.Should().Be(survivor.Id);
        (await _dbContext.ConsumeAuditEntries.AsNoTracking()
            .SingleAsync(r => r.Id == consumeAuditEntry.Id)).CanonicalIngredientId.Should().Be(survivor.Id);
    }

    // Ten CanonicalIngredient rows ship via CanonicalIngredientConfiguration.HasData
    // (tomatoes, corn, zucchini, strawberries, apples, kale, asparagus,
    // peaches, pumpkin, broccoli) so every test starts with that baseline.
    private const int SeededCanonicalCount = 10;

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private CanonicalIngredient SeedCanonicalIngredient(string name)
    {
        var row = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = UnitOfMeasureConfiguration.EachId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(row);
        return row;
    }

    private Recipe SeedRecipe()
    {
        var row = new Recipe
        {
            CuisineType = "",
            Id = Guid.NewGuid(),
            Instructions = "step",
            ServingCount = 1,
            Title = "test"
        };
        _dbContext.Recipes.Add(row);
        return row;
    }

    private MealPlan SeedMealPlan()
    {
        var row = new MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "test plan",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.Add(row);
        return row;
    }

    private MealPlanSlot SeedMealPlanSlot(Guid mealPlanId, Guid recipeId)
    {
        var row = new MealPlanSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            Id = Guid.NewGuid(),
            MealPlanId = mealPlanId,
            MealSlot = MealSlot.Dinner,
            RecipeId = recipeId
        };
        _dbContext.MealPlanSlots.Add(row);
        return row;
    }

    private RecipeIngredient SeedRecipeIngredient(Guid canonicalIngredientId, Guid recipeId)
    {
        var row = new RecipeIngredient
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            IsContainerResolved = true,
            Quantity = 1m,
            RecipeId = recipeId
        };
        _dbContext.RecipeIngredients.Add(row);
        return row;
    }

    private InventoryItem SeedInventoryItem(Guid canonicalIngredientId)
    {
        var row = new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Pantry,
            Quantity = 1m,
            UnitOfMeasureId = UnitOfMeasureConfiguration.EachId
        };
        _dbContext.InventoryItems.Add(row);
        return row;
    }

    private ShoppingListItem SeedShoppingListItem(Guid canonicalIngredientId)
    {
        var row = new ShoppingListItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            MealPlanId = null,
            Quantity = 1m,
            UnitOfMeasureId = UnitOfMeasureConfiguration.EachId
        };
        _dbContext.ShoppingListItems.Add(row);
        return row;
    }

    private SeasonalityWindow SeedSeasonalityWindow(Guid canonicalIngredientId)
    {
        var row = new SeasonalityWindow
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            PeakSeasonEnd = Month.September,
            PeakSeasonStart = Month.June,
            UsdaZone = "7a"
        };
        _dbContext.SeasonalityWindows.Add(row);
        return row;
    }

    private ConsumeAuditEntry SeedConsumeAuditEntry(Guid canonicalIngredientId, Guid mealPlanSlotId)
    {
        var row = new ConsumeAuditEntry
        {
            CanonicalIngredientId = canonicalIngredientId,
            CreatedAt = DateTime.UtcNow,
            DeductedQuantity = 100m,
            Id = Guid.NewGuid(),
            MealPlanSlotId = mealPlanSlotId,
            OriginalInventoryItemId = null,
            OriginalLocation = StorageLocation.Pantry,
            UnitOfMeasureId = UnitOfMeasureConfiguration.EachId
        };
        _dbContext.ConsumeAuditEntries.Add(row);
        return row;
    }
}
