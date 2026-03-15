// Feature: UOM Display Conversion
//
// Scenario: Imperial default when no UserPreferences row exists
//   Given no UserPreferences row is in the database
//   When ConvertAsync is called with a weight quantity
//   Then the result is returned in Imperial units (oz or lb)
//   And no exception is thrown
//
// Scenario: Weight below 454g displays as oz
//   Given the display system is Imperial
//   And the quantity is 453g
//   When ConvertAsync is called with UomType.Weight
//   Then the abbreviation is "oz"
//
// Scenario: Weight at 454g displays as lb (boundary)
//   Given the display system is Imperial
//   And the quantity is 454g
//   When ConvertAsync is called with UomType.Weight
//   Then the abbreviation is "lb"
//
// Scenario: Volume below 59ml displays as fl oz
//   Given the display system is Imperial
//   And the quantity is 58ml
//   When ConvertAsync is called with UomType.Volume
//   Then the abbreviation is "fl oz"
//
// Scenario: Volume at 59ml displays as cups (lower boundary)
//   Given the display system is Imperial
//   And the quantity is 59ml
//   When ConvertAsync is called with UomType.Volume
//   Then the abbreviation is "cups"
//
// Scenario: Volume at 946ml displays as cups (upper boundary)
//   Given the display system is Imperial
//   And the quantity is 946ml
//   When ConvertAsync is called with UomType.Volume
//   Then the abbreviation is "cups"
//
// Scenario: Volume above 946ml displays as quarts
//   Given the display system is Imperial
//   And the quantity is 947ml
//   When ConvertAsync is called with UomType.Volume
//   Then the abbreviation is "qt"
//
// Scenario: Count (ea) passes through unchanged in both display systems
//   Given the display system is Imperial
//   When ConvertAsync is called with UomType.Count
//   Then the abbreviation is "ea" and quantity is unchanged
//
// Scenario: Arbitrary UomType passes through as ea
//   Given the display system is Imperial
//   When ConvertAsync is called with UomType.Arbitrary
//   Then the abbreviation is "ea" and quantity is unchanged
//
// Scenario: Metric display returns ml for Volume
//   Given the display system is Metric
//   When ConvertAsync is called with UomType.Volume
//   Then the abbreviation is "ml" and quantity is unchanged
//
// Scenario: Metric display returns g for Weight
//   Given the display system is Metric
//   When ConvertAsync is called with UomType.Weight
//   Then the abbreviation is "g" and quantity is unchanged
//
// Scenario: Imperial fl oz conversion math is correct
//   Given the display system is Imperial
//   And the quantity is 29.574ml
//   When ConvertAsync is called with UomType.Volume
//   Then the returned quantity is approximately 1.00 fl oz

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Common;

public class UomDisplayConverterTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MealsEnPlaceDbContext CreateDbContext(string dbName) =>
        new(new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static UomDisplayConverter BuildConverter(MealsEnPlaceDbContext dbContext) =>
        new(dbContext);

    // ── Imperial default — no UserPreferences row (QA spec edge case #12) ────

    [Fact]
    public async Task ConvertAsync_NoUserPreferencesRow_DefaultsToImperialWithoutThrowing()
    {
        // Arrange — empty database, no UserPreferences row seeded
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_NoUserPreferencesRow_DefaultsToImperialWithoutThrowing));
        var converter = BuildConverter(dbContext);

        // Act
        var act = async () => await converter.ConvertAsync(500m, UomType.Weight);

        // Assert — must not throw; result must be in Imperial (oz or lb)
        await act.Should().NotThrowAsync();
        var (_, abbreviation) = await converter.ConvertAsync(500m, UomType.Weight);
        abbreviation.Should().BeOneOf("oz", "lb");
    }

    [Fact]
    public async Task ConvertAsync_NoUserPreferencesRow_ReturnsImperialAbbreviation()
    {
        // Arrange
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_NoUserPreferencesRow_ReturnsImperialAbbreviation));
        var converter = BuildConverter(dbContext);

        // Act — 500g is above the 454g threshold, should return lb
        var (_, abbreviation) = await converter.ConvertAsync(500m, UomType.Weight);

        // Assert
        abbreviation.Should().Be("lb");
    }

    // ── Weight threshold boundary: oz vs lb (QA spec edge case #13) ──────────

    [Fact]
    public async Task ConvertAsync_Weight453g_ReturnsOz()
    {
        // Arrange — 453g is below the 454g threshold
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Weight453g_ReturnsOz));
        var converter = BuildConverter(dbContext);

        // Act
        var (_, abbreviation) = await converter.ConvertAsync(453m, UomType.Weight);

        // Assert
        abbreviation.Should().Be("oz");
    }

    [Fact]
    public async Task ConvertAsync_Weight454g_ReturnsLb()
    {
        // Arrange — 454g is exactly at the lb threshold
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Weight454g_ReturnsLb));
        var converter = BuildConverter(dbContext);

        // Act
        var (_, abbreviation) = await converter.ConvertAsync(454m, UomType.Weight);

        // Assert
        abbreviation.Should().Be("lb");
    }

    [Fact]
    public async Task ConvertAsync_Weight455g_ReturnsLb()
    {
        // Arrange — 455g is above the threshold
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Weight455g_ReturnsLb));
        var converter = BuildConverter(dbContext);

        // Act
        var (_, abbreviation) = await converter.ConvertAsync(455m, UomType.Weight);

        // Assert
        abbreviation.Should().Be("lb");
    }

    // ── Volume threshold boundaries (QA spec edge case #14) ──────────────────

    [Fact]
    public async Task ConvertAsync_Volume58ml_ReturnsFlOz()
    {
        // Arrange — 58ml is below the 59ml cups boundary
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Volume58ml_ReturnsFlOz));
        var converter = BuildConverter(dbContext);

        // Act
        var (_, abbreviation) = await converter.ConvertAsync(58m, UomType.Volume);

        // Assert
        abbreviation.Should().Be("fl oz");
    }

    [Fact]
    public async Task ConvertAsync_Volume59ml_ReturnsCups()
    {
        // Arrange — 59ml is exactly at the cups lower boundary
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Volume59ml_ReturnsCups));
        var converter = BuildConverter(dbContext);

        // Act
        var (_, abbreviation) = await converter.ConvertAsync(59m, UomType.Volume);

        // Assert
        abbreviation.Should().Be("cups");
    }

    [Fact]
    public async Task ConvertAsync_Volume946ml_ReturnsCups()
    {
        // Arrange — 946ml is exactly at the upper cups boundary (inclusive)
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Volume946ml_ReturnsCups));
        var converter = BuildConverter(dbContext);

        // Act
        var (_, abbreviation) = await converter.ConvertAsync(946m, UomType.Volume);

        // Assert
        abbreviation.Should().Be("cups");
    }

    [Fact]
    public async Task ConvertAsync_Volume947ml_ReturnsQuarts()
    {
        // Arrange — 947ml is above the 946ml cups boundary
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Volume947ml_ReturnsQuarts));
        var converter = BuildConverter(dbContext);

        // Act
        var (_, abbreviation) = await converter.ConvertAsync(947m, UomType.Volume);

        // Assert
        abbreviation.Should().Be("qt");
    }

    // ── ea / Arbitrary passthrough ────────────────────────────────────────────

    [Fact]
    public async Task ConvertAsync_CountType_ReturnsEaWithUnchangedQuantity()
    {
        // Arrange
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_CountType_ReturnsEaWithUnchangedQuantity));
        var converter = BuildConverter(dbContext);
        const decimal inputQuantity = 12m;

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(inputQuantity, UomType.Count);

        // Assert
        abbreviation.Should().Be("ea");
        displayQuantity.Should().Be(inputQuantity);
    }

    [Fact]
    public async Task ConvertAsync_ArbitraryType_ReturnsEaWithUnchangedQuantity()
    {
        // Arrange
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_ArbitraryType_ReturnsEaWithUnchangedQuantity));
        var converter = BuildConverter(dbContext);
        const decimal inputQuantity = 3m;

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(inputQuantity, UomType.Arbitrary);

        // Assert
        abbreviation.Should().Be("ea");
        displayQuantity.Should().Be(inputQuantity);
    }

    // ── Metric display mode returns base units unchanged ──────────────────────

    [Fact]
    public async Task ConvertAsync_MetricDisplayVolume_ReturnsMlUnchanged()
    {
        // Arrange
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_MetricDisplayVolume_ReturnsMlUnchanged));
        dbContext.UserPreferences.Add(new UserPreferences { Id = Guid.NewGuid(), DisplaySystem = DisplaySystem.Metric });
        await dbContext.SaveChangesAsync();
        var converter = BuildConverter(dbContext);
        const decimal inputQuantity = 250m;

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(inputQuantity, UomType.Volume);

        // Assert
        abbreviation.Should().Be("ml");
        displayQuantity.Should().Be(inputQuantity);
    }

    [Fact]
    public async Task ConvertAsync_MetricDisplayWeight_ReturnsGUnchanged()
    {
        // Arrange
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_MetricDisplayWeight_ReturnsGUnchanged));
        dbContext.UserPreferences.Add(new UserPreferences { Id = Guid.NewGuid(), DisplaySystem = DisplaySystem.Metric });
        await dbContext.SaveChangesAsync();
        var converter = BuildConverter(dbContext);
        const decimal inputQuantity = 500m;

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(inputQuantity, UomType.Weight);

        // Assert
        abbreviation.Should().Be("g");
        displayQuantity.Should().Be(inputQuantity);
    }

    [Fact]
    public async Task ConvertAsync_MetricDisplayCount_ReturnsEaUnchanged()
    {
        // Arrange
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_MetricDisplayCount_ReturnsEaUnchanged));
        dbContext.UserPreferences.Add(new UserPreferences { Id = Guid.NewGuid(), DisplaySystem = DisplaySystem.Metric });
        await dbContext.SaveChangesAsync();
        var converter = BuildConverter(dbContext);
        const decimal inputQuantity = 6m;

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(inputQuantity, UomType.Count);

        // Assert
        abbreviation.Should().Be("ea");
        displayQuantity.Should().Be(inputQuantity);
    }

    // ── Imperial conversion math correctness ──────────────────────────────────

    [Fact]
    public async Task ConvertAsync_Volume29Point574ml_ReturnsApproximately1FlOz()
    {
        // Arrange — 29.574ml = 1 fl oz
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Volume29Point574ml_ReturnsApproximately1FlOz));
        var converter = BuildConverter(dbContext);

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(29.574m, UomType.Volume);

        // Assert
        abbreviation.Should().Be("fl oz");
        displayQuantity.Should().BeApproximately(1.00m, 0.01m);
    }

    [Fact]
    public async Task ConvertAsync_Volume236Point588ml_ReturnsApproximately1Cup()
    {
        // Arrange — 236.588ml = 1 cup
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Volume236Point588ml_ReturnsApproximately1Cup));
        var converter = BuildConverter(dbContext);

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(236.588m, UomType.Volume);

        // Assert
        abbreviation.Should().Be("cups");
        displayQuantity.Should().BeApproximately(1.00m, 0.01m);
    }

    [Fact]
    public async Task ConvertAsync_Weight28Point350g_ReturnsApproximately1Oz()
    {
        // Arrange — 28.350g = 1 oz
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Weight28Point350g_ReturnsApproximately1Oz));
        var converter = BuildConverter(dbContext);

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(28.350m, UomType.Weight);

        // Assert
        abbreviation.Should().Be("oz");
        displayQuantity.Should().BeApproximately(1.00m, 0.01m);
    }

    [Fact]
    public async Task ConvertAsync_Weight453Point592g_ReturnsOzBelowThreshold()
    {
        // Arrange — 453.592g is below the 454g threshold, so it stays in oz
        await using var dbContext = CreateDbContext(nameof(ConvertAsync_Weight453Point592g_ReturnsOzBelowThreshold));
        var converter = BuildConverter(dbContext);

        // Act
        var (displayQuantity, abbreviation) = await converter.ConvertAsync(453.592m, UomType.Weight);

        // Assert
        abbreviation.Should().Be("oz");
        displayQuantity.Should().BeApproximately(16.00m, 0.01m);
    }

}
