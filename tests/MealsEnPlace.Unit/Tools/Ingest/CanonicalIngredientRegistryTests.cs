// Feature: CanonicalIngredientRegistry
//
// Scenario: GetOrCreate returns existing id for cached name
//   Given an existing CanonicalIngredient "Salt" in the DB
//   When GetOrCreate("Salt") is called
//   Then the existing id is returned and no new row is added
//
// Scenario: GetOrCreate inserts a new row for a novel name
//   Given no CanonicalIngredient for "Cardamom"
//   When GetOrCreate("Cardamom") is called
//   Then a new row is added to the DbContext (pending save)
//   And NewRowsCreated is incremented
//
// Scenario: GetOrCreate matches case-insensitively
//   Given an existing CanonicalIngredient "salt"
//   When GetOrCreate("SALT") is called
//   Then the existing id is returned, no new row added
//
// Scenario: GetOrCreate with empty or whitespace defaults to "unknown"
//   Given an empty or whitespace input
//   When GetOrCreate is called
//   Then the "unknown" canonical is inserted on first call, reused on subsequent
//
// Scenario: PickBestNerMatch returns the longest whole-word substring match
//   Given raw "3 1/2 c. bite size shredded rice biscuits"
//   And NER tokens ["rice", "rice biscuits", "size"]
//   When PickBestNerMatch is called
//   Then "rice biscuits" is returned (longest match wins)
//
// Scenario: PickBestNerMatch requires whole-word boundary
//   Given raw "1/2 c. broken pecans"
//   And NER tokens ["can"]
//   When PickBestNerMatch is called
//   Then null is returned ("can" is a substring of "pecans" but not whole-word)
//
// Scenario: PickBestNerMatch returns null when no NER token is contained
//   Given raw "1 cup sugar"
//   And NER tokens ["flour", "butter"]
//   When PickBestNerMatch is called
//   Then null is returned
//
// Scenario: PickBestNerMatch ignores empty NER tokens
//   Given NER tokens with a null or empty entry mixed with valid ones
//   When PickBestNerMatch is called
//   Then empties are skipped and the valid match is returned

using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using MealsEnPlace.Tools.Ingest;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class CanonicalIngredientRegistryTests : IDisposable
{
    private readonly MealsEnPlaceDbContext _dbContext;

    public CanonicalIngredientRegistryTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);

        // The registry requires the default-each unit of measure to exist at load time.
        _dbContext.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Abbreviation = "ea",
            ConversionFactor = 1.0m,
            Id = UnitOfMeasureConfiguration.EachId,
            Name = "Each",
            UnitOfMeasureType = UnitOfMeasureType.Count
        });
        _dbContext.SaveChanges();
    }

    public void Dispose() => _dbContext.Dispose();

    private void SeedCanonical(string name, Guid id)
    {
        _dbContext.CanonicalIngredients.Add(new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = UnitOfMeasureConfiguration.EachId,
            Id = id,
            Name = name
        });
        _dbContext.SaveChanges();
    }

    // ── GetOrCreate behavior ──────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreate_ExistingName_ReturnsExistingIdWithoutInserting()
    {
        var existingId = Guid.NewGuid();
        SeedCanonical("Salt", existingId);

        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        var returned = registry.GetOrCreate("Salt");

        returned.Should().Be(existingId);
        registry.NewRowsCreated.Should().Be(0);
    }

    [Fact]
    public async Task GetOrCreate_NovelName_InsertsAndIncrementsCounter()
    {
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        var returned = registry.GetOrCreate("Cardamom");

        returned.Should().NotBe(Guid.Empty);
        registry.NewRowsCreated.Should().Be(1);

        // Pending-add should be in the change tracker (not yet saved).
        _dbContext.CanonicalIngredients.Local
            .Should().ContainSingle(c => c.Name == "Cardamom");
    }

    [Fact]
    public async Task GetOrCreate_CaseInsensitiveMatchToExisting_ReturnsExistingId()
    {
        var existingId = Guid.NewGuid();
        SeedCanonical("salt", existingId);

        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        var returned = registry.GetOrCreate("SALT");

        returned.Should().Be(existingId);
        registry.NewRowsCreated.Should().Be(0);
    }

    [Fact]
    public async Task GetOrCreate_EmptyInput_InsertsUnknownOnceAndReusesOnSubsequentCalls()
    {
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        var first = registry.GetOrCreate(string.Empty);
        var second = registry.GetOrCreate("   ");

        first.Should().Be(second);
        registry.NewRowsCreated.Should().Be(1);
    }

    // ── PickBestNerMatch ──────────────────────────────────────────────────────

    [Fact]
    public void PickBestNerMatch_LongestMatchWins()
    {
        var raw = "3 1/2 c. bite size shredded rice biscuits";
        var ner = new[] { "rice", "rice biscuits", "size" };

        var best = CanonicalIngredientRegistry.PickBestNerMatch(raw, ner);

        best.Should().Be("rice biscuits");
    }

    [Fact]
    public void PickBestNerMatch_RequiresWholeWordBoundary()
    {
        var raw = "1/2 c. broken pecans";
        var ner = new[] { "can" }; // "can" is a substring of "pecans" but not whole-word

        var best = CanonicalIngredientRegistry.PickBestNerMatch(raw, ner);

        best.Should().BeNull();
    }

    [Fact]
    public void PickBestNerMatch_NoMatch_ReturnsNull()
    {
        var raw = "1 cup sugar";
        var ner = new[] { "flour", "butter" };

        var best = CanonicalIngredientRegistry.PickBestNerMatch(raw, ner);

        best.Should().BeNull();
    }

    [Fact]
    public void PickBestNerMatch_IgnoresEmptyNerEntries()
    {
        var raw = "1 cup sugar";
        var ner = new[] { string.Empty, "sugar", "   " };

        var best = CanonicalIngredientRegistry.PickBestNerMatch(raw, ner);

        best.Should().Be("sugar");
    }

    [Fact]
    public void PickBestNerMatch_EmptyRaw_ReturnsNull()
    {
        var best = CanonicalIngredientRegistry.PickBestNerMatch(string.Empty, ["sugar"]);

        best.Should().BeNull();
    }

    [Fact]
    public void PickBestNerMatch_EmptyNerList_ReturnsNull()
    {
        var best = CanonicalIngredientRegistry.PickBestNerMatch("1 cup sugar", []);

        best.Should().BeNull();
    }

    [Fact]
    public void PickBestNerMatch_IsCaseInsensitive()
    {
        var raw = "1 CUP Sugar";
        var ner = new[] { "sugar" };

        var best = CanonicalIngredientRegistry.PickBestNerMatch(raw, ner);

        best.Should().Be("sugar");
    }

    // Scenario: GetOrCreate truncates over-length NER tokens to the 200-char column cap
    //   Given an NER token longer than CanonicalIngredient.Name's HasMaxLength
    //   When GetOrCreate is called twice with the same over-length token
    //   Then exactly one row is added
    //   And its Name is truncated to 200 chars (no overflow on SaveChanges)

    [Fact]
    public async Task GetOrCreate_OverLengthNerToken_TruncatesAndDedupes()
    {
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);
        var longToken = new string('z', 250);

        var firstId = registry.GetOrCreate(longToken);
        var secondId = registry.GetOrCreate(longToken);
        await _dbContext.SaveChangesAsync();

        firstId.Should().Be(secondId);
        var saved = await _dbContext.CanonicalIngredients.AsNoTracking()
            .SingleAsync(ci => ci.Id == firstId);
        saved.Name.Length.Should().Be(200);
        registry.NewRowsCreated.Should().Be(1);
    }
}
