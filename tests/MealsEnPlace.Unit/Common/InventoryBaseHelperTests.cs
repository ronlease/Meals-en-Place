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
//   Given an inventory item whose UOM conversion fails
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

    private readonly Mock<IUomConversionService> _conversionServiceMock = new(MockBehavior.Strict);

    // ── ConvertToBaseUnitsAsync — successful conversions ──────────────────────

    [Fact]
    public async Task ConvertToBaseUnitsAsync_MultipleItemsSameIngredient_ReturnsSingleKeyWithBothEntries()
    {
        // Arrange
        var ingredientId = Guid.NewGuid();
        var uomId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, uomId, 100m),
            BuildInventoryItem(ingredientId, uomId, 200m)
        };
        SetupSuccessfulConversion(uomId, 1.0m, UomType.Weight);

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
        var uomId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, uomId, 100m),
            BuildInventoryItem(ingredientId, uomId, 200m)
        };
        _conversionServiceMock
            .SetupSequence(s => s.ConvertToBaseUnitsAsync(It.IsAny<decimal>(), uomId, It.IsAny<CancellationToken>()))
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
        var uomId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId1, uomId, 50m),
            BuildInventoryItem(ingredientId2, uomId, 75m)
        };
        SetupSuccessfulConversion(uomId, 1.0m, UomType.Weight);

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
        var uomId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, uomId, 100m)
        };
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(100m, uomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversionResult.NotFound(uomId));

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
        var goodUomId = Guid.NewGuid();
        var badUomId = Guid.NewGuid();
        var items = new List<InventoryItem>
        {
            BuildInventoryItem(ingredientId, goodUomId, 100m),
            BuildInventoryItem(ingredientId, badUomId, 200m)
        };
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(100m, goodUomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversionResult.Ok(100m, "g", "g"));
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(200m, badUomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConversionResult.NotFound(badUomId));

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
        var uomId = Guid.NewGuid();
        var expiry = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var items = new List<InventoryItem>
        {
            BuildInventoryItemWithExpiry(ingredientId, uomId, 100m, expiry)
        };
        SetupSuccessfulConversion(uomId, 1.0m, UomType.Weight);

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

    private static InventoryItem BuildInventoryItem(Guid ingredientId, Guid uomId, decimal quantity) =>
        BuildInventoryItemWithExpiry(ingredientId, uomId, quantity, null);

    private static InventoryItem BuildInventoryItemWithExpiry(
        Guid ingredientId, Guid uomId, decimal quantity, DateOnly? expiry)
    {
        var uom = new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1.0m,
            Id = uomId,
            Name = "gram",
            UomType = UomType.Weight
        };

        return new InventoryItem
        {
            CanonicalIngredient = new CanonicalIngredient
            {
                Category = IngredientCategory.Other,
                DefaultUomId = uomId,
                Id = ingredientId,
                Name = "Test Ingredient"
            },
            CanonicalIngredientId = ingredientId,
            ExpiryDate = expiry,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Pantry,
            Quantity = quantity,
            Uom = uom,
            UomId = uomId
        };
    }

    private void SetupSuccessfulConversion(Guid uomId, decimal factor, UomType uomType)
    {
        _conversionServiceMock
            .Setup(s => s.ConvertToBaseUnitsAsync(It.IsAny<decimal>(), uomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal qty, Guid _, CancellationToken _) =>
                ConversionResult.Ok(qty * factor, "g", uomType == UomType.Volume ? "ml" : "g"));
    }
}
