// Feature: InventoryBaseHelper — ConvertToBaseUnitsAsync
//
// Scenario: Multiple items — all convert successfully — returns grouped dictionary
//   Given two inventory items belonging to the same canonical ingredient
//   When ConvertToBaseUnitsAsync is called
//   Then both items are aggregated under the same key
//   And each entry holds the converted base quantity
//
// Scenario: Multiple items — different ingredients — each gets its own key
//   Given two inventory items belonging to different canonical ingredients
//   When ConvertToBaseUnitsAsync is called
//   Then the dictionary contains two separate entries
//
// Scenario: Failed conversion — item is skipped
//   Given an inventory item whose unit of measure conversion fails
//   When ConvertToBaseUnitsAsync is called
//   Then that item is omitted from the result
//
// Scenario: Grouping by ingredient — quantities accumulate per ingredient
//   Given three items, two sharing the same canonical ingredient
//   When ConvertToBaseUnitsAsync is called
//   Then the shared ingredient key has two InventoryBaseEntry records

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Models.Entities;
using Moq;

namespace MealsEnPlace.Unit.Common;

public class InventoryBaseHelperTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IUnitOfMeasureConversionService> _conversionServiceMock = new(MockBehavior.Strict);

    // ── ConvertToBaseUnitsAsync — successful conversions ──────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_MultipleItemsSameIngredient_ReturnsSingleKeyWithBothEntries()
    {
        // Arrange
        var ingredientId = Guid.NewGuid();
        var unitOfMeasureId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, unitOfMeasureId, 100m),
            BuildInventoryItem(ingredientId, unitOfMeasureId, 200m)
        };
        SetupSuccessfulConversion(unitOfMeasureId, 1.0m, UnitOfMeasureType.Weight);

        // Act
        var result = await InventoryBaseHelper.ConvertToBaseUnitsAsync(
            items, _conversionServiceMock.Object, CancellationToken.None);

        // Assert
        result.Should().ContainKey(ingredientId);
        result[ingredientId].Should().HaveCount(2);
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_MultipleItemsSameIngredient_BaseQuantitiesAreCorrect()
    {
        // Arrange
        var ingredientId = Guid.NewGuid();
        var unitOfMeasureId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, unitOfMeasureId, 100m),
            BuildInventoryItem(ingredientId, unitOfMeasureId, 200m)
        };
        _conversionServiceMock
            .SetupSequence(s => s.ConvertToBaseUnitsAsync(It.IsAny<decimal>(), unitOfMeasureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversionResult.Ok(100m, "g", "g"))
            .ReturnsAsync(ConversionResult.Ok(200m, "g", "g"));

        // Act
        var result = await InventoryBaseHelper.ConvertToBaseUnitsAsync(
            items, _conversionServiceMock.Object, CancellationToken.None);

        // Assert
        var entries = result[ingredientId];
        entries.Select(e => e.BaseQuantity).Should().BeEquivalentTo([100m, 200m]);
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_DifferentIngredients_ReturnsTwoKeys()
    {
        // Arrange
        var ingredientId1 = Guid.NewGuid();
        var ingredientId2 = Guid.NewGuid();
        var unitOfMeasureId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId1, unitOfMeasureId, 50m),
            BuildInventoryItem(ingredientId2, unitOfMeasureId, 75m)
        };
        SetupSuccessfulConversion(unitOfMeasureId, 1.0m, UnitOfMeasureType.Weight);

        // Act
        var result = await InventoryBaseHelper.ConvertToBaseUnitsAsync(
            items, _conversionServiceMock.Object, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey(ingredientId1);
        result.Should().ContainKey(ingredientId2);
    }

    // ── ConvertToBaseUnitsAsync — failed conversion ───────────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_FailedConversion_ItemIsSkipped()
    {
        // Arrange
        var ingredientId = Guid.NewGuid();
        var unitOfMeasureId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, unitOfMeasureId, 100m)
        };
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(100m, unitOfMeasureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversionResult.NotFound(unitOfMeasureId));

        // Act
        var result = await InventoryBaseHelper.ConvertToBaseUnitsAsync(
            items, _conversionServiceMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_OneSuccessOneFailure_OnlySuccessfulItemIsIncluded()
    {
        // Arrange
        var ingredientId = Guid.NewGuid();
        var goodUnitOfMeasureId = Guid.NewGuid();
        var badUnitOfMeasureId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, goodUnitOfMeasureId, 100m),
            BuildInventoryItem(ingredientId, badUnitOfMeasureId, 200m)
        };
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(100m, goodUnitOfMeasureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversionResult.Ok(100m, "g", "g"));
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(200m, badUnitOfMeasureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversionResult.NotFound(badUnitOfMeasureId));

        // Act
        var result = await InventoryBaseHelper.ConvertToBaseUnitsAsync(
            items, _conversionServiceMock.Object, CancellationToken.None);

        // Assert
        result.Should().ContainKey(ingredientId);
        result[ingredientId].Should().HaveCount(1);
        result[ingredientId][0].BaseQuantity.Should().Be(100m);
    }

    // ── ConvertToBaseUnitsAsync — expiry date propagation ────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_ItemWithExpiryDate_EntryPreservesExpiryDate()
    {
        // Arrange
        var ingredientId = Guid.NewGuid();
        var unitOfMeasureId = Guid.NewGuid();
        var expiry = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var items = new List<InventoryItem>
        {
            BuildInventoryItemWithExpiry(ingredientId, unitOfMeasureId, 100m, expiry)
        };
        SetupSuccessfulConversion(unitOfMeasureId, 1.0m, UnitOfMeasureType.Weight);

        // Act
        var result = await InventoryBaseHelper.ConvertToBaseUnitsAsync(
            items, _conversionServiceMock.Object, CancellationToken.None);

        // Assert
        result[ingredientId][0].ExpiryDate.Should().Be(expiry);
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_EmptyList_ReturnsEmptyDictionary()
    {
        // Act
        var result = await InventoryBaseHelper.ConvertToBaseUnitsAsync(
            [], _conversionServiceMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static InventoryItem BuildInventoryItem(Guid ingredientId, Guid unitOfMeasureId, decimal quantity) =>
        BuildInventoryItemWithExpiry(ingredientId, unitOfMeasureId, quantity, null);

    private static InventoryItem BuildInventoryItemWithExpiry(
        Guid ingredientId, Guid unitOfMeasureId, decimal quantity, DateOnly? expiry)
    {
        var unitOfMeasure = new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1.0m,
            Id = unitOfMeasureId,
            Name = "gram",
            UnitOfMeasureType = UnitOfMeasureType.Weight
        };

        return new InventoryItem
        {
            CanonicalIngredient = new CanonicalIngredient
            {
                Category = IngredientCategory.Other,
                DefaultUnitOfMeasureId = unitOfMeasureId,
                Id = ingredientId,
                Name = "Test Ingredient"
            },
            CanonicalIngredientId = ingredientId,
            ExpiryDate = expiry,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Pantry,
            Quantity = quantity,
            UnitOfMeasure = unitOfMeasure,
            UnitOfMeasureId = unitOfMeasureId
        };
    }

    private void SetupSuccessfulConversion(Guid unitOfMeasureId, decimal factor, UnitOfMeasureType unitOfMeasureType)
    {
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(It.IsAny<decimal>(), unitOfMeasureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal qty, Guid _, CancellationToken _) =>
                ConversionResult.Ok(qty * factor, "g", unitOfMeasureType == UnitOfMeasureType.Volume ? "ml" : "g"));
    }
}
