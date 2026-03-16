// Feature: UOM Normalization — Conversion Service
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
// Scenario: Unknown fromUomId returns not-found failure
//   Given a Guid that does not exist in the database
//   When ConvertToBaseUnitsAsync is called
//   Then Success is false
//   And ErrorMessage references the missing Guid
//
// Scenario: Unknown fromUomId in ConvertBetweenAsync returns not-found failure
//   Given a fromUomId Guid that does not exist in the database
//   When ConvertBetweenAsync is called
//   Then Success is false
//   And ConvertedQuantity is 0
//
// Scenario: Unknown toUomId in ConvertBetweenAsync returns not-found failure
//   Given a valid fromUomId but a toUomId Guid that does not exist
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

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Common;

public class UomConversionServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MealsEnPlaceDbContext CreateSeededDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var dbContext = new MealsEnPlaceDbContext(options);

        // Seed all canonical UOM rows using the same fixed Guids from UnitOfMeasureConfiguration.
        // Base units first (no BaseUomId dependency).
        dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.EachId,
                Name = "Each",
                UomType = UomType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "g",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.GramId,
                Name = "Gram",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "ml",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.MlId,
                Name = "Milliliter",
                UomType = UomType.Volume
            },
            // Volume units
            new UnitOfMeasure
            {
                Abbreviation = "cup",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 236.588m,
                Id = UnitOfMeasureConfiguration.CupId,
                Name = "Cup",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "fl oz",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 29.574m,
                Id = UnitOfMeasureConfiguration.FlOzId,
                Name = "Fluid Ounce",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "L",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 1000.0m,
                Id = UnitOfMeasureConfiguration.LiterId,
                Name = "Liter",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "pt",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 473.176m,
                Id = UnitOfMeasureConfiguration.PintId,
                Name = "Pint",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "qt",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 946.353m,
                Id = UnitOfMeasureConfiguration.QuartId,
                Name = "Quart",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tbsp",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 14.787m,
                Id = UnitOfMeasureConfiguration.TbspId,
                Name = "Tablespoon",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tsp",
                BaseUomId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 4.929m,
                Id = UnitOfMeasureConfiguration.TspId,
                Name = "Teaspoon",
                UomType = UomType.Volume
            },
            // Weight units
            new UnitOfMeasure
            {
                Abbreviation = "kg",
                BaseUomId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 1000.0m,
                Id = UnitOfMeasureConfiguration.KgId,
                Name = "Kilogram",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "lb",
                BaseUomId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 453.592m,
                Id = UnitOfMeasureConfiguration.LbId,
                Name = "Pound",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "oz",
                BaseUomId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 28.350m,
                Id = UnitOfMeasureConfiguration.OzId,
                Name = "Ounce",
                UomType = UomType.Weight
            }
        );

        dbContext.SaveChanges();
        return dbContext;
    }

    private static UomConversionService BuildService(MealsEnPlaceDbContext dbContext) =>
        new(dbContext);

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
        result.ToUom.Should().Be("ml");
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
        result.ToUom.Should().Be("ea");
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
        result.ToUom.Should().Be("g");
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
        result.ToUom.Should().Be("ml");
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
        result.ToUom.Should().Be("g");
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
        result.ToUom.Should().Be("ml");
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
        result.FromUom.Should().Be("tsp");
        result.ToUom.Should().Be("ml");
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
        result.FromUom.Should().Be("oz");
        result.ToUom.Should().Be("g");
    }

    // ── ConvertToBaseUnitsAsync — unknown UOM ─────────────────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_UnknownUomId_ReturnsNotFoundFailure()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertToBaseUnitsAsync_UnknownUomId_ReturnsNotFoundFailure));
        var service = BuildService(dbContext);
        var unknownId = Guid.NewGuid();

        // Act
        var result = await service.ConvertToBaseUnitsAsync(1m, unknownId);

        // Assert
        result.Success.Should().BeFalse();
        result.ConvertedQuantity.Should().Be(0m);
        result.ErrorMessage.Should().Contain(unknownId.ToString());
    }

    // ── ConvertBetweenAsync — cross-type rejection (QA edge case #1) ──────────

    [Fact]
    public async Task ConvertBetweenAsync_CupToGram_ReturnsCrossTypeFailureNeverNumericValue()
    {
        // Arrange — critical edge case: Volume (cup) to Weight (g) must always fail
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_CupToGram_ReturnsCrossTypeFailureNeverNumericValue));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertBetweenAsync(
            1m,
            UnitOfMeasureConfiguration.CupId,
            UnitOfMeasureConfiguration.GramId);

        // Assert — must be a failure; ConvertedQuantity must be zero, not a guessed value
        result.Success.Should().BeFalse();
        result.ConvertedQuantity.Should().Be(0m);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConvertBetweenAsync_CountToWeight_ReturnsCrossTypeFailure()
    {
        // Arrange — Count (ea) to Weight (g) is an incompatible cross-type conversion
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_CountToWeight_ReturnsCrossTypeFailure));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertBetweenAsync(
            3m,
            UnitOfMeasureConfiguration.EachId,
            UnitOfMeasureConfiguration.GramId);

        // Assert
        result.Success.Should().BeFalse();
        result.ConvertedQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task ConvertBetweenAsync_WeightToVolume_ReturnsCrossTypeFailure()
    {
        // Arrange — Weight (oz) to Volume (ml) is an incompatible cross-type conversion
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_WeightToVolume_ReturnsCrossTypeFailure));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertBetweenAsync(
            8m,
            UnitOfMeasureConfiguration.OzId,
            UnitOfMeasureConfiguration.MlId);

        // Assert
        result.Success.Should().BeFalse();
        result.ConvertedQuantity.Should().Be(0m);
    }

    // ── ConvertBetweenAsync — compatible units ────────────────────────────────

    [Fact]
    public async Task ConvertBetweenAsync_CupToMl_ReturnsCorrectQuantity()
    {
        // Arrange — 1 cup = 236.588 ml
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_CupToMl_ReturnsCorrectQuantity));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertBetweenAsync(
            1m,
            UnitOfMeasureConfiguration.CupId,
            UnitOfMeasureConfiguration.MlId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(236.588m, 0.001m);
    }

    [Fact]
    public async Task ConvertBetweenAsync_LbToOz_ReturnsApproximately16Oz()
    {
        // Arrange — 1 lb = 453.592 g base; oz factor = 28.350; 453.592 / 28.350 ≈ 16
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_LbToOz_ReturnsApproximately16Oz));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertBetweenAsync(
            1m,
            UnitOfMeasureConfiguration.LbId,
            UnitOfMeasureConfiguration.OzId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(16.0m, 0.01m);
    }

    [Fact]
    public async Task ConvertBetweenAsync_TspToTbsp_ThreeTspIsApproximatelyOneTbsp()
    {
        // Arrange — 3 tsp * 4.929 = 14.787 ml base; 14.787 / 14.787 = 1 tbsp
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_TspToTbsp_ThreeTspIsApproximatelyOneTbsp));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertBetweenAsync(
            3m,
            UnitOfMeasureConfiguration.TspId,
            UnitOfMeasureConfiguration.TbspId);

        // Assert
        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().BeApproximately(1.0m, 0.01m);
    }

    // ── ConvertBetweenAsync — unknown UOM IDs ─────────────────────────────────

    [Fact]
    public async Task ConvertBetweenAsync_UnknownFromUomId_ReturnsNotFoundFailure()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_UnknownFromUomId_ReturnsNotFoundFailure));
        var service = BuildService(dbContext);
        var unknownId = Guid.NewGuid();

        // Act
        var result = await service.ConvertBetweenAsync(
            1m,
            unknownId,
            UnitOfMeasureConfiguration.GramId);

        // Assert
        result.Success.Should().BeFalse();
        result.ConvertedQuantity.Should().Be(0m);
        result.ErrorMessage.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public async Task ConvertBetweenAsync_UnknownToUomId_ReturnsNotFoundFailure()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_UnknownToUomId_ReturnsNotFoundFailure));
        var service = BuildService(dbContext);
        var unknownId = Guid.NewGuid();

        // Act
        var result = await service.ConvertBetweenAsync(
            1m,
            UnitOfMeasureConfiguration.OzId,
            unknownId);

        // Assert
        result.Success.Should().BeFalse();
        result.ConvertedQuantity.Should().Be(0m);
        result.ErrorMessage.Should().Contain(unknownId.ToString());
    }

    // ── ConvertBetweenAsync — result metadata ─────────────────────────────────

    [Fact]
    public async Task ConvertBetweenAsync_CompatibleUnits_FromUomAndToUomPopulated()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(ConvertBetweenAsync_CompatibleUnits_FromUomAndToUomPopulated));
        var service = BuildService(dbContext);

        // Act
        var result = await service.ConvertBetweenAsync(
            1m,
            UnitOfMeasureConfiguration.OzId,
            UnitOfMeasureConfiguration.GramId);

        // Assert
        result.Success.Should().BeTrue();
        result.FromUom.Should().Be("oz");
        result.ToUom.Should().Be("g");
    }
}
