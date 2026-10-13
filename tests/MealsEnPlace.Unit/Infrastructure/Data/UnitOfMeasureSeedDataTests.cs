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
