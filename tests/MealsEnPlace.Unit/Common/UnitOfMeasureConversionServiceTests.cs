// Feature: unit of measure Normalization — Conversion Service
//
// Scenario: Convert a known volume unit (tsp) to its base unit (ml)
//   Given 1 tsp with a ConversionFactor of 4.929
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is 4.929 ml
//   And Success is true
//
// Scenario: Convert a known weight unit (oz) to its base unit (g)
//   Given 1 oz with a ConversionFactor of 28.350
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is 28.350 g
//   And Success is true
//
// Scenario: Convert the base unit (ml) to itself
//   Given a quantity in ml
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is unchanged
//   And Success is true
//
// Scenario: Convert the base unit (g) to itself
//   Given a quantity in g
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is unchanged
//   And Success is true
//
// Scenario: Convert the count unit (ea) to itself
//   Given a quantity in ea
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is unchanged
//   And Success is true
//
// Scenario: Cross-type conversion — Volume to Weight — returns failure, never a numeric value (QA edge case #1)
//   Given a quantity in cup (Volume)
//   And a target unit of g (Weight)
//   When ConvertBetweenAsync is called
//   Then Success is false
//   And ConvertedQuantity is 0
//   And ErrorMessage describes the incompatibility
//
// Scenario: Cross-type conversion — Weight to Volume — returns failure
//   Given a quantity in oz (Weight)
//   And a target unit of ml (Volume)
//   When ConvertBetweenAsync is called
//   Then Success is false
//   And ConvertedQuantity is 0
//
// Scenario: Cross-type conversion — Count to Weight — returns failure
//   Given a quantity in ea (Count)
//   And a target unit of g (Weight)
//   When ConvertBetweenAsync is called
//   Then Success is false
//   And ConvertedQuantity is 0
//
// Scenario: Convert between two compatible volume units (cup to ml via base)
//   Given a quantity in cup
//   When ConvertBetweenAsync targets ml
//   Then Success is true
//   And the returned quantity equals quantity * 236.588
//
// Scenario: Convert between two compatible volume units (tsp to tbsp via base)
//   Given 3 tsp (3 * 4.929 = 14.787 ml base) and tbsp factor 14.787
//   When ConvertBetweenAsync targets tbsp
//   Then Success is true
//   And the returned quantity is approximately 1 tbsp
//
// Scenario: Convert between two compatible weight units (lb to oz via base)
//   Given 1 lb (453.592 g base) and oz factor 28.350
//   When ConvertBetweenAsync targets oz
//   Then Success is true
//   And the returned quantity is approximately 16 oz
//
// Scenario: Unknown fromUnitOfMeasureId returns not-found failure
//   Given a Guid that does not exist in the database
//   When ConvertToBaseUnitsAsync is called
//   Then Success is false
//   And ErrorMessage references the missing Guid
//
// Scenario: Unknown fromUnitOfMeasureId in ConvertBetweenAsync returns not-found failure
//   Given a fromUnitOfMeasureId Guid that does not exist in the database
//   When ConvertBetweenAsync is called
//   Then Success is false
//   And ConvertedQuantity is 0
//
// Scenario: Unknown toUnitOfMeasureId in ConvertBetweenAsync returns not-found failure
//   Given a valid fromUnitOfMeasureId but a toUnitOfMeasureId Guid that does not exist
//   When ConvertBetweenAsync is called
//   Then Success is false
//   And ConvertedQuantity is 0
//
// Scenario: Convert tbsp to base unit ml
//   Given 1 tbsp with ConversionFactor 14.787
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is 14.787 ml
//   And Success is true
//
// Scenario: Convert lb to base unit g
//   Given 1 lb with ConversionFactor 453.592
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is 453.592 g
//   And Success is true
//
// Scenario: Convert cup to base unit ml
//   Given 2 cups with ConversionFactor 236.588
//   When ConvertToBaseUnitsAsync is called
//   Then the returned quantity is 473.176 ml
//   And Success is true
//
// Scenario: Repeated calls within one scope do not re-query the UnitsOfMeasure table (MEP-061)
//   Given the cache has been warmed by a prior ConvertToBaseUnitsAsync call on a known unit of measure ID
//   And the underlying UnitsOfMeasure row for that ID is mutated directly in the store afterward
//   When ConvertToBaseUnitsAsync is called again for the same ID on the same service instance
//   Then the returned quantity reflects the OLD, pre-mutation ConversionFactor
//   And this proves no second query was issued against the mutated store
//
// Scenario: A new scope does not see stale data after a UnitsOfMeasure correction (MEP-061 acceptance criterion 3)
//   Given a service instance has already cached the UnitsOfMeasure table and converted using the old factor
//   And the ConversionFactor for that unit is corrected directly in the underlying store afterward
//   When a brand-new UnitOfMeasureConversionService instance — representing a new request scope — converts the same ID
//   Then the returned quantity reflects the CORRECTED ConversionFactor
//
// Scenario: An unknown unit of measure ID after the cache is warm does not poison the cache
//   Given the cache has been warmed by a successful conversion on a known unit of measure ID
//   When ConvertToBaseUnitsAsync is called with an unknown Guid
//   Then Success is false, ConvertedQuantity is 0, and ErrorMessage references the unknown Guid
//   And a subsequent conversion on the known unit of measure ID still succeeds
//
// Scenario: An unknown unit of measure ID as the very first call still loads the full table
//   Given a fresh service instance that has not yet cached anything
//   When ConvertToBaseUnitsAsync is called first with an unknown Guid
//   Then Success is false
//   And a subsequent conversion on a known unit of measure ID succeeds, proving the full table was loaded on the first call

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Common;

public class UnitOfMeasureConversionServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MealsEnPlaceDbContext CreateSeededDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var dbContext = new MealsEnPlaceDbContext(options);

        // Seed all canonical unit of measure rows using the same fixed Guids from UnitOfMeasureConfiguration.
        // Base units first (no BaseUnitOfMeasureId dependency).
        dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.EachId,
                Name = "Each",
                UnitOfMeasureType = UnitOfMeasureType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "g",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.GramId,
                Name = "Gram",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "ml",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.MlId,
                Name = "Milliliter",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            // Volume units
            new UnitOfMeasure
            {
                Abbreviation = "cup",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 236.588m,
                Id = UnitOfMeasureConfiguration.CupId,
                Name = "Cup",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "fl oz",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 29.574m,
                Id = UnitOfMeasureConfiguration.FlOzId,
                Name = "Fluid Ounce",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "L",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 1000.0m,
                Id = UnitOfMeasureConfiguration.LiterId,
                Name = "Liter",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "pt",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 473.176m,
                Id = UnitOfMeasureConfiguration.PintId,
                Name = "Pint",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "qt",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 946.353m,
                Id = UnitOfMeasureConfiguration.QuartId,
                Name = "Quart",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tbsp",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 14.787m,
                Id = UnitOfMeasureConfiguration.TbspId,
                Name = "Tablespoon",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tsp",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 4.929m,
                Id = UnitOfMeasureConfiguration.TspId,
                Name = "Teaspoon",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            // Weight units
            new UnitOfMeasure
            {
                Abbreviation = "kg",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 1000.0m,
                Id = UnitOfMeasureConfiguration.KgId,
                Name = "Kilogram",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "lb",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 453.592m,
                Id = UnitOfMeasureConfiguration.LbId,
                Name = "Pound",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "oz",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 28.350m,
                Id = UnitOfMeasureConfiguration.OzId,
                Name = "Ounce",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            }
        );

        dbContext.SaveChanges();
        return dbContext;
    }

    private static UnitOfMeasureConversionService BuildService(MealsEnPlaceDbContext dbContext) =>
        new(dbContext);

    /// <summary>
    /// Opens a new, unseeded <see cref="MealsEnPlaceDbContext"/> against an already-seeded
    /// in-memory database identified by <paramref name="dbName"/>. Used to simulate a brand-new
    /// dependency injection scope (i.e. a new HTTP request) reading the same underlying store.
    /// </summary>
    private static MealsEnPlaceDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new MealsEnPlaceDbContext(options);
    }

    // ── ConvertToBaseUnitsAsync — factor correctness ──────────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneCup_Returns236Point588Ml()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneCup_Returns236Point588Ml));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(2m, UnitOfMeasureConfiguration.CupId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(473.176m, 0.001m);
        result.ToUnitOfMeasure.Should().Be("ml");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneEa_ReturnsUnchangedQuantity()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneEa_ReturnsUnchangedQuantity));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(6m, UnitOfMeasureConfiguration.EachId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().Be(6m);
        result.ToUnitOfMeasure.Should().Be("ea");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneLb_Returns453Point592g()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneLb_Returns453Point592g));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.LbId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(453.592m, 0.001m);
        result.ToUnitOfMeasure.Should().Be("g");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneMl_ReturnsUnchangedQuantity()
    {
        // Arrange — ml is already the base unit; ConversionFactor is 1.0
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneMl_ReturnsUnchangedQuantity));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(250m, UnitOfMeasureConfiguration.MlId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().Be(250m);
        result.ToUnitOfMeasure.Should().Be("ml");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneGram_ReturnsUnchangedQuantity()
    {
        // Arrange — g is already the base unit; ConversionFactor is 1.0
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneGram_ReturnsUnchangedQuantity));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(100m, UnitOfMeasureConfiguration.GramId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().Be(100m);
        result.ToUnitOfMeasure.Should().Be("g");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneTbsp_Returns14Point787Ml()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneTbsp_Returns14Point787Ml));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.TbspId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(14.787m, 0.001m);
        result.ToUnitOfMeasure.Should().Be("ml");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneTsp_Returns4Point929Ml()
    {
        // Arrange — seed ConversionFactor for tsp is 4.929; verify factor exactly
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneTsp_Returns4Point929Ml));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.TspId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(4.929m, 0.001m);
        result.FromUnitOfMeasure.Should().Be("tsp");
        result.ToUnitOfMeasure.Should().Be("ml");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneOz_Returns28Point350g()
    {
        // Arrange — seed ConversionFactor for oz is 28.350; verify factor exactly
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_OneOz_Returns28Point350g));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.OzId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(28.350m, 0.001m);
        result.FromUnitOfMeasure.Should().Be("oz");
        result.ToUnitOfMeasure.Should().Be("g");
    }

    // ── ConvertToBaseUnitsAsync — unknown unit of measure ─────────────────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_UnknownUnitOfMeasureId_ReturnsNotFoundFailure()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_UnknownUnitOfMeasureId_ReturnsNotFoundFailure));
        var service = BuildService(dbContext);
        var unknownId = Guid.NewGuid();

        // Act
        var result = await service.ConvertToBaseUnitsAsync(1m, unknownId);

        // Assert
        result.Success.Should().BeFalse();
        result.ConvertedQuantity.Should().Be(0m);
        result.ErrorMessage.Should().Contain(unknownId.ToString());
    }

    // ── Per-scope caching (MEP-061) ───────────────────────────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_CalledTwiceOnSameInstanceAfterUnderlyingMutation_ReturnsOldCachedFactor()
    {
        // Arrange
        var dbName = nameof(ConvertToBaseUnitsAsync_CalledTwiceOnSameInstanceAfterUnderlyingMutation_ReturnsOldCachedFactor);
        await using var dbContext = CreateSeededDbContext(dbName);
        var service = BuildService(dbContext);

        // Act — warm the cache with the original factor
        var firstResult = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.OzId);

        // Mutate the underlying store directly, bypassing the service's cache
        var ounce = await dbContext.UnitsOfMeasure.SingleAsync(u => u.Id == UnitOfMeasureConfiguration.OzId);
        ounce.ConversionFactor = 999m;
        await dbContext.SaveChangesAsync();

        // Convert the same ID again on the SAME service instance
        var secondResult = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.OzId);

        // Assert — both calls reflect the OLD, pre-mutation factor, proving no re-query occurred
        firstResult.ConvertedQuantity.Should().BeApproximately(28.350m, 0.001m);
        secondResult.ConvertedQuantity.Should().BeApproximately(28.350m, 0.001m);
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_NewServiceInstanceAfterUnderlyingMutation_ReflectsCorrectedFactor()
    {
        // Arrange
        var dbName = nameof(ConvertToBaseUnitsAsync_NewServiceInstanceAfterUnderlyingMutation_ReflectsCorrectedFactor);
        await using var dbContext = CreateSeededDbContext(dbName);
        var service = BuildService(dbContext);

        // Warm the first instance's cache with the original factor
        await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.OzId);

        // Correct the ConversionFactor directly in the underlying store
        var ounce = await dbContext.UnitsOfMeasure.SingleAsync(u => u.Id == UnitOfMeasureConfiguration.OzId);
        ounce.ConversionFactor = 100m;
        await dbContext.SaveChangesAsync();

        // Act — a brand-new service instance backed by a brand-new DbContext (a new request scope)
        // against the same underlying in-memory database
        await using var newScopeDbContext = CreateDbContext(dbName);
        var newScopeService = BuildService(newScopeDbContext);
        var result = await newScopeService.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.OzId);

        // Assert — the new scope sees the corrected factor, proving the cache does not outlive its scope
        result.ConvertedQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_UnknownIdAfterCacheIsWarm_DoesNotPoisonCache()
    {
        // Arrange
        var dbName = nameof(ConvertToBaseUnitsAsync_UnknownIdAfterCacheIsWarm_DoesNotPoisonCache);
        await using var dbContext = CreateSeededDbContext(dbName);
        var service = BuildService(dbContext);
        var unknownId = Guid.NewGuid();

        // Act — warm the cache with a successful conversion on a known ID
        var warmResult = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.OzId);

        // Convert an unknown ID on the same, already-warm instance
        var missResult = await service.ConvertToBaseUnitsAsync(1m, unknownId);

        // Convert the known ID again on the same instance
        var secondKnownResult = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.OzId);

        // Assert
        warmResult.Success.Should().BeTrue();
        missResult.Success.Should().BeFalse();
        missResult.ConvertedQuantity.Should().Be(0m);
        missResult.ErrorMessage.Should().Contain(unknownId.ToString());
        secondKnownResult.Success.Should().BeTrue();
        secondKnownResult.ConvertedQuantity.Should().BeApproximately(28.350m, 0.001m);
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_UnknownIdAsFirstCall_StillLoadsFullTableForSubsequentKnownId()
    {
        // Arrange
        var dbName = nameof(ConvertToBaseUnitsAsync_UnknownIdAsFirstCall_StillLoadsFullTableForSubsequentKnownId);
        await using var dbContext = CreateSeededDbContext(dbName);
        var service = BuildService(dbContext);
        var unknownId = Guid.NewGuid();

        // Act — unknown ID is the very first call on a fresh instance
        var firstResult = await service.ConvertToBaseUnitsAsync(1m, unknownId);

        // A known ID converted afterward on the same instance
        var secondResult = await service.ConvertToBaseUnitsAsync(1m, UnitOfMeasureConfiguration.CupId);

        // Assert
        firstResult.Success.Should().BeFalse();
        secondResult.Success.Should().BeTrue();
        secondResult.ConvertedQuantity.Should().BeApproximately(236.588m, 0.001m);
    }
}
