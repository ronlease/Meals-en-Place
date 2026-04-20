// Feature: Mark Meal as Eaten with Optional Inventory Auto-Deplete (MEP-027)
// Feature: Auto-Restore Inventory When a Consumed Meal is Unmarked (MEP-031)
//
// Scenario: Consuming a slot with auto-deplete OFF is a state-only change
//   Given AutoDepleteOnConsume is false
//   When the user marks the slot as eaten
//   Then ConsumedAt is set
//   And ConsumedWithAutoDeplete is false
//   And no InventoryItem rows are modified
//   And no ConsumeAuditEntry rows are created
//
// Scenario: Consuming a slot with auto-deplete ON deducts ingredients oldest-expiry-first
//   Given AutoDepleteOnConsume is true
//   And the recipe calls for 200g Chicken
//   And there are two Chicken rows: one 100g expiring 2026-05-01 and one 500g expiring 2026-06-01
//   When the user marks the slot as eaten
//   Then the earlier-expiry row depletes to 0 first
//   And the second row depletes by the remaining 100g
//   And two ConsumeAuditEntry rows are written, one per decrement
//
// Scenario: Insufficient inventory surfaces a warning but does not block the consume
//   Given auto-deplete is on
//   And the recipe calls for 500g of flour
//   And total flour in inventory is only 300g across all rows
//   When the user marks the slot as eaten
//   Then inventory flour is depleted to 0g (not below)
//   And a ShortIngredient entry reports the 200g shortage
//   And the consume still succeeds
//
// Scenario: Unconsume restores each decrement back to its original InventoryItem row
//   Given a slot has been consumed with auto-deplete on
//   And the original rows still exist
//   When the user unmarks the slot
//   Then each audited decrement is added back to the exact row it came from
//   And the slot's ConsumedAt and ConsumedWithAutoDeplete are cleared
//   And the ConsumeAuditEntry rows are removed
//
// Scenario: Unconsume creates a replacement row when the original was deleted
//   Given a slot has been consumed with auto-deplete on
//   And the original InventoryItem row has since been deleted
//   When the user unmarks the slot
//   Then a new InventoryItem row is created with the audited quantity, location, and expiry
//
// Scenario: Unmarking a slot that was consumed with auto-deplete OFF is a state-only change
//   Given a slot has ConsumedAt set
//   And ConsumedWithAutoDeplete is false
//   When the user unmarks the slot
//   Then ConsumedAt is cleared
//   And no InventoryItem rows are modified

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.MealPlan;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.MealPlan;

public sealed class MealConsumptionServiceTests : IDisposable
{
    private static readonly Guid GramId = UnitOfMeasureConfiguration.GramId;

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly MealConsumptionService _sut;

    public MealConsumptionServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        SeedGramUnit();
        _sut = new MealConsumptionService(_dbContext, new UnitOfMeasureConversionService(_dbContext));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ConsumeAsync_WithAutoDepleteOff_MarksStateOnly_NoInventoryChange()
    {
        // Arrange
        SetAutoDeplete(false);
        var chicken = SeedIngredient("Chicken");
        var row = SeedInventoryItem(chicken.Id, 500m);
        var slot = SeedSlotForRecipe("Grilled Chicken", (chicken.Id, 200m));

        // Act
        var result = await _sut.ConsumeAsync(slot.Id);

        // Assert — state-only
        result.Should().NotBeNull();
        result!.AutoDepleteApplied.Should().BeFalse();
        result.ShortIngredients.Should().BeEmpty();

        var persistedSlot = await _dbContext.MealPlanSlots.SingleAsync(s => s.Id == slot.Id);
        persistedSlot.ConsumedAt.Should().NotBeNull();
        persistedSlot.ConsumedWithAutoDeplete.Should().BeFalse();

        (await _dbContext.InventoryItems.SingleAsync(i => i.Id == row.Id)).Quantity.Should().Be(500m);
        (await _dbContext.ConsumeAuditEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ConsumeAsync_WithAutoDepleteOn_DepletesOldestExpiryFirst_WritesAuditRows()
    {
        // Arrange
        SetAutoDeplete(true);
        var chicken = SeedIngredient("Chicken");
        // Two rows; earlier expiry must deplete first.
        var earlyRow = SeedInventoryItem(chicken.Id, 100m, DateOnly.Parse("2026-05-01"));
        var lateRow = SeedInventoryItem(chicken.Id, 500m, DateOnly.Parse("2026-06-01"));
        var slot = SeedSlotForRecipe("Grilled Chicken", (chicken.Id, 200m));

        // Act
        var result = await _sut.ConsumeAsync(slot.Id);

        // Assert
        result!.AutoDepleteApplied.Should().BeTrue();
        result.ShortIngredients.Should().BeEmpty();

        (await _dbContext.InventoryItems.SingleAsync(i => i.Id == earlyRow.Id)).Quantity.Should().Be(0m);
        (await _dbContext.InventoryItems.SingleAsync(i => i.Id == lateRow.Id)).Quantity.Should().Be(400m);

        var auditEntries = await _dbContext.ConsumeAuditEntries
            .Where(a => a.MealPlanSlotId == slot.Id)
            .ToListAsync();
        auditEntries.Should().HaveCount(2);
        auditEntries.Single(a => a.OriginalInventoryItemId == earlyRow.Id).DeductedQuantity.Should().Be(100m);
        auditEntries.Single(a => a.OriginalInventoryItemId == lateRow.Id).DeductedQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task ConsumeAsync_WithInsufficientInventory_ClampsToZeroAndReportsShortage()
    {
        // Arrange
        SetAutoDeplete(true);
        var flour = SeedIngredient("Flour");
        var row = SeedInventoryItem(flour.Id, 300m);
        var slot = SeedSlotForRecipe("Bread", (flour.Id, 500m));

        // Act
        var result = await _sut.ConsumeAsync(slot.Id);

        // Assert — consume succeeds, inventory clamps to 0, shortage reported
        result!.AutoDepleteApplied.Should().BeTrue();
        (await _dbContext.InventoryItems.SingleAsync(i => i.Id == row.Id)).Quantity.Should().Be(0m);

        result.ShortIngredients.Should().HaveCount(1);
        result.ShortIngredients[0].IngredientName.Should().Be("Flour");
        result.ShortIngredients[0].ShortBy.Should().Be(200m);
        result.ShortIngredients[0].UnitOfMeasureAbbreviation.Should().Be("g");
    }

    [Fact]
    public async Task UnconsumeAsync_WithAutoDepleteAudit_RestoresToOriginalRows()
    {
        // Arrange
        SetAutoDeplete(true);
        var chicken = SeedIngredient("Chicken");
        var row = SeedInventoryItem(chicken.Id, 500m);
        var slot = SeedSlotForRecipe("Grilled Chicken", (chicken.Id, 200m));
        await _sut.ConsumeAsync(slot.Id);
        (await _dbContext.InventoryItems.SingleAsync(i => i.Id == row.Id)).Quantity.Should().Be(300m);

        // Act
        var found = await _sut.UnconsumeAsync(slot.Id);

        // Assert
        found.Should().BeTrue();
        (await _dbContext.InventoryItems.SingleAsync(i => i.Id == row.Id)).Quantity.Should().Be(500m);
        (await _dbContext.ConsumeAuditEntries.CountAsync(a => a.MealPlanSlotId == slot.Id)).Should().Be(0);

        var persistedSlot = await _dbContext.MealPlanSlots.SingleAsync(s => s.Id == slot.Id);
        persistedSlot.ConsumedAt.Should().BeNull();
        persistedSlot.ConsumedWithAutoDeplete.Should().BeNull();
    }

    [Fact]
    public async Task UnconsumeAsync_WhenOriginalRowDeleted_CreatesReplacementRowFromAudit()
    {
        // Arrange
        SetAutoDeplete(true);
        var chicken = SeedIngredient("Chicken");
        var row = SeedInventoryItem(chicken.Id, 500m, DateOnly.Parse("2026-05-15"));
        var slot = SeedSlotForRecipe("Grilled Chicken", (chicken.Id, 200m));
        await _sut.ConsumeAsync(slot.Id);
        // User empties the row (remaining 300g) and deletes it.
        var persistedRow = await _dbContext.InventoryItems.SingleAsync(i => i.Id == row.Id);
        _dbContext.InventoryItems.Remove(persistedRow);
        await _dbContext.SaveChangesAsync();

        // Act
        var found = await _sut.UnconsumeAsync(slot.Id);

        // Assert — a fresh row is created carrying the audited data
        found.Should().BeTrue();
        var restored = await _dbContext.InventoryItems.SingleAsync(i => i.CanonicalIngredientId == chicken.Id);
        restored.Quantity.Should().Be(200m);
        restored.Location.Should().Be(StorageLocation.Fridge);
        restored.ExpiryDate.Should().Be(DateOnly.Parse("2026-05-15"));
        restored.Id.Should().NotBe(row.Id);
    }

    [Fact]
    public async Task UnconsumeAsync_StateOnlyConsume_DoesNotModifyInventory()
    {
        // Arrange
        SetAutoDeplete(false);
        var chicken = SeedIngredient("Chicken");
        var row = SeedInventoryItem(chicken.Id, 500m);
        var slot = SeedSlotForRecipe("Grilled Chicken", (chicken.Id, 200m));
        await _sut.ConsumeAsync(slot.Id);

        // Act
        var found = await _sut.UnconsumeAsync(slot.Id);

        // Assert — state-only reversal
        found.Should().BeTrue();
        (await _dbContext.InventoryItems.SingleAsync(i => i.Id == row.Id)).Quantity.Should().Be(500m);
        var persistedSlot = await _dbContext.MealPlanSlots.SingleAsync(s => s.Id == slot.Id);
        persistedSlot.ConsumedAt.Should().BeNull();
        persistedSlot.ConsumedWithAutoDeplete.Should().BeNull();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedGramUnit()
    {
        _dbContext.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1m,
            Id = GramId,
            Name = "Gram",
            UnitOfMeasureType = UnitOfMeasureType.Weight
        });
        _dbContext.SaveChanges();
    }

    private CanonicalIngredient SeedIngredient(string name)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = GramId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        _dbContext.SaveChanges();
        return ingredient;
    }

    private InventoryItem SeedInventoryItem(
        Guid canonicalIngredientId,
        decimal quantity,
        DateOnly? expiryDate = null)
    {
        var item = new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            ExpiryDate = expiryDate,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Fridge,
            Quantity = quantity,
            UnitOfMeasureId = GramId
        };
        _dbContext.InventoryItems.Add(item);
        _dbContext.SaveChanges();
        return item;
    }

    private MealPlanSlot SeedSlotForRecipe(
        string title,
        params (Guid IngredientId, decimal Quantity)[] ingredientLines)
    {
        var recipe = new Recipe
        {
            CuisineType = "Test",
            Id = Guid.NewGuid(),
            Instructions = "Cook.",
            ServingCount = 1,
            Title = title
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.SaveChanges();

        foreach (var (ingredientId, qty) in ingredientLines)
        {
            _dbContext.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ingredientId,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = qty,
                RecipeId = recipe.Id,
                UnitOfMeasureId = GramId
            });
        }

        var plan = new MealsEnPlace.Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.Add(plan);
        _dbContext.SaveChanges();

        var slot = new MealPlanSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            Id = Guid.NewGuid(),
            MealPlanId = plan.Id,
            MealSlot = MealSlot.Dinner,
            RecipeId = recipe.Id
        };
        _dbContext.MealPlanSlots.Add(slot);
        _dbContext.SaveChanges();
        return slot;
    }

    private void SetAutoDeplete(bool value)
    {
        var prefs = _dbContext.UserPreferences.FirstOrDefault();
        if (prefs is null)
        {
            _dbContext.UserPreferences.Add(new UserPreferences
            {
                AutoDepleteOnConsume = value,
                DisplaySystem = DisplaySystem.Imperial,
                Id = Guid.NewGuid()
            });
        }
        else
        {
            prefs.AutoDepleteOnConsume = value;
        }
        _dbContext.SaveChanges();
    }
}
