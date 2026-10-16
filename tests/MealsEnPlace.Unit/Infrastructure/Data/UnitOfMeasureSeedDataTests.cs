// Feature: Seeded unit of measure and alias data (MEP-067 WP-A)
//
// Scenario: Count-style words map to Each
//   Given the seeded aliases clove, cloves, slice, slices, bunch, bunches, stick, sticks
//   Then each alias points at the Each unit
//
// Scenario: Plural pinch and dash words map to their units
//   Given the seeded aliases pinches and dashes
//   Then they point at the Pinch and Dash units respectively
//
// Scenario: Pinch and dash are Volume units based on ml
//   Given the seeded units pinch and dash
//   Then pinch has factor 0.31 and dash has factor 0.62, both Volume with base ml
//
// Scenario: Gallon is a Volume unit based on ml
//   Given the seeded unit gal
//   Then it is named Gallon with factor 3785.41, Volume, base ml
//
// Scenario: Gallon aliases map to Gallon
//   Given the seeded aliases gal., gals, gals., gallon, gallons
//   Then each points at the Gallon unit
//
// Scenario: 1/2 gal. converts to about 1892.7 ml and resolves in both resolvers
//   Given the real seed data
//   When "1/2 gal. vanilla ice cream" is resolved by the API service and the ingest resolver
//   Then both resolve to 0.5 gal and ConvertToBaseUnitsAsync gives 1892.705 ml
//
// Scenario: Pinch or dash without a number resolves in both resolvers against the real seed
//   Given "pinch of salt", "dash of salt", "dash of pepper", "pinch of red pepper"
//   Then both resolvers return 1 pinch (salt) or 1 dash (pepper)
//
// Scenario: No seeded alias is a container word
//   Given every seeded alias
//   Then ContainerReferenceDetector.Detect finds no container reference
//
// Scenario: 2 dash converts to 1.24 ml
//   Given 2 dash
//   When ConvertToBaseUnitsAsync is called
//   Then the result is 1.24 ml

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using MealsEnPlace.Tools.Ingest;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Infrastructure.Data;

public sealed class UnitOfMeasureSeedDataTests : IDisposable
{
    private readonly MealsEnPlaceDbContext _dbContext;

    public UnitOfMeasureSeedDataTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase($"uom-seed-{Guid.NewGuid()}")
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ConvertToBaseUnitsAsync_TwoDash_ReturnsOnePointTwoFourMilliliters()
    {
        var service = new UnitOfMeasureConversionService(_dbContext);

        var result = await service.ConvertToBaseUnitsAsync(2m, UnitOfMeasureConfiguration.DashId);

        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().Be(1.24m);
        result.ToUnitOfMeasure.Should().Be("ml");
    }

    [Fact]
    public async Task ConvertToBaseUnitsAsync_HalfGallon_ReturnsAboutOneThousandEightHundredNinetyTwoMilliliters()
    {
        var service = new UnitOfMeasureConversionService(_dbContext);

        var result = await service.ConvertToBaseUnitsAsync(0.5m, UnitOfMeasureConfiguration.GallonId);

        result.Success.Should().BeTrue();
        result.ConvertedQuantity.Should().Be(1892.705m);
        result.ToUnitOfMeasure.Should().Be("ml");
    }

    [Theory]
    [InlineData("1/2 gal. vanilla ice cream", "vanilla ice cream", 0.5)]
    [InlineData("1 gal. water", "water", 1)]
    [InlineData("2 gals milk", "milk", 2)]
    [InlineData("2 gallons milk", "milk", 2)]
    [InlineData("1 gallon milk", "milk", 1)]
    public async Task Gallon_SeededData_ResolvesInBothResolvers(
        string measureString, string ingredientName, double expectedQuantity)
    {
        var ingestResolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);
        var ingestResult = ingestResolver.NormalizeOrDefer(measureString, ingredientName);

        var service = new UnitOfMeasureNormalizationService(null!, null!, _dbContext);
        var serviceResult = await service.TryResolveDeterministicallyAsync(measureString, ingredientName);

        ingestResult.WasDeferred.Should().BeFalse();
        ingestResult.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.GallonId);
        ingestResult.Quantity.Should().Be((decimal)expectedQuantity);
        serviceResult.Should().NotBeNull();
        serviceResult!.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.GallonId);
        serviceResult.Quantity.Should().Be((decimal)expectedQuantity);
    }

    [Theory]
    [InlineData("pinch of salt", "salt", "pinch")]
    [InlineData("dash of salt", "salt", "pinch")]
    [InlineData("dash of pepper", "pepper", "dash")]
    [InlineData("pinch of red pepper", "red pepper", "dash")]
    public async Task PinchOrDashWithoutNumber_SeededData_ResolvesInBothResolvers(
        string measureString, string ingredientName, string expectedAbbreviation)
    {
        var ingestResolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);
        var ingestResult = ingestResolver.NormalizeOrDefer(measureString, ingredientName);

        var service = new UnitOfMeasureNormalizationService(null!, null!, _dbContext);
        var serviceResult = await service.TryResolveDeterministicallyAsync(measureString, ingredientName);

        ingestResult.WasDeferred.Should().BeFalse();
        ingestResult.UnitOfMeasureAbbreviation.Should().Be(expectedAbbreviation);
        ingestResult.Quantity.Should().Be(1m);
        serviceResult.Should().NotBeNull();
        serviceResult!.UnitOfMeasureAbbreviation.Should().Be(expectedAbbreviation);
        serviceResult.Quantity.Should().Be(1m);
    }

    [Theory]
    [InlineData("gal.")]
    [InlineData("gallon")]
    [InlineData("gallons")]
    [InlineData("gals")]
    [InlineData("gals.")]
    public async Task SeededAlias_GallonForms_MapToGallon(string alias)
    {
        var row = await _dbContext.UnitOfMeasureAliases.SingleAsync(a => a.Alias == alias);

        row.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.GallonId);
    }

    [Fact]
    public async Task SeededUnit_Gallon_IsVolumeBasedOnMilliliter()
    {
        var unit = await _dbContext.UnitsOfMeasure.SingleAsync(u => u.Abbreviation == "gal");

        unit.ConversionFactor.Should().Be(3785.41m);
        unit.Name.Should().Be("Gallon");
        unit.UnitOfMeasureType.Should().Be(UnitOfMeasureType.Volume);
        unit.BaseUnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.MlId);
    }

    [Fact]
    public async Task SeededAlias_Dashes_MapsToDash()
    {
        var row = await _dbContext.UnitOfMeasureAliases.SingleAsync(a => a.Alias == "dashes");

        row.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.DashId);
    }

    [Fact]
    public async Task SeededAlias_Pinches_MapsToPinch()
    {
        var row = await _dbContext.UnitOfMeasureAliases.SingleAsync(a => a.Alias == "pinches");

        row.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.PinchId);
    }

    [Theory]
    [InlineData("bunch")]
    [InlineData("bunches")]
    [InlineData("clove")]
    [InlineData("cloves")]
    [InlineData("slice")]
    [InlineData("slices")]
    [InlineData("stick")]
    [InlineData("sticks")]
    public async Task SeededAlias_CountStyleWord_MapsToEach(string alias)
    {
        var row = await _dbContext.UnitOfMeasureAliases.SingleAsync(a => a.Alias == alias);

        row.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
    }

    [Fact]
    public async Task SeededAliases_AllOfThem_AreNotContainerWords()
    {
        var aliases = await _dbContext.UnitOfMeasureAliases.Select(a => a.Alias).ToListAsync();

        aliases.Should().NotBeEmpty();
        aliases.Where(a => ContainerReferenceDetector.Detect(a).IsContainerReference)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("dash", 0.62)]
    [InlineData("pinch", 0.31)]
    public async Task SeededUnit_PinchAndDash_AreVolumeBasedOnMilliliter(string abbreviation, double factor)
    {
        var unit = await _dbContext.UnitsOfMeasure.SingleAsync(u => u.Abbreviation == abbreviation);

        unit.ConversionFactor.Should().Be((decimal)factor);
        unit.UnitOfMeasureType.Should().Be(UnitOfMeasureType.Volume);
        unit.BaseUnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.MlId);
    }
}
