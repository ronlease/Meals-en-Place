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

    // Scenario: Truncation at the 200-char boundary must not leave a trailing space
    //   Given a NER token of 199 'z' characters followed by " b" (201 chars total)
    //   When GetOrCreate truncates it to 200 chars
    //   Then the stored name does not end with a space
    // NOTE: This test exposes a source defect — GetOrCreate slices to 200 chars without
    // trimming, so a token whose 200th character is a space produces a name with a
    // trailing space.  The test is left failing to flag the defect.

    [Fact]
    public async Task GetOrCreate_OverLengthTokenWithSpaceAtBoundary_StoredNameHasNoTrailingSpace()
    {
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        // 199 'z' chars + ' ' + 'b' = 201 chars total.
        // Index 199 (the 200th character) is a space; truncation to 200 chars without
        // a subsequent TrimEnd yields a name ending with a space.
        var token = new string('z', 199) + " b";

        var id = registry.GetOrCreate(token);
        await _dbContext.SaveChangesAsync();

        // Assert
        var saved = await _dbContext.CanonicalIngredients.AsNoTracking()
            .SingleAsync(ci => ci.Id == id);
        saved.Name.Should().NotEndWith(" ");
    }

    // Scenario: Punctuation variants and the clean token all resolve to the same id via normalization
    //   Given no existing CanonicalIngredient for "apple"
    //   When GetOrCreate is called with "apple.", "apple/", and "apple"
    //   Then all three return the same id
    //   And exactly one new row is created

    [Fact]
    public async Task GetOrCreate_PunctuationVariantsAndCleanToken_AllReturnSameId()
    {
        // Arrange
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        // Act — "apple.", "apple/" and "apple" all normalize to "apple"
        var idFromPeriod = registry.GetOrCreate("apple.");
        var idFromSlash = registry.GetOrCreate("apple/");
        var idFromClean = registry.GetOrCreate("apple");

        // Assert
        idFromPeriod.Should().Be(idFromSlash);
        idFromSlash.Should().Be(idFromClean);
        registry.NewRowsCreated.Should().Be(1);
    }

    // Scenario: Rejected NER token falls back to "unknown" canonical and does not create a separate row
    //   Given a NER token that normalizes to a rejection (e.g., "and" → StopwordsOnly)
    //   When GetOrCreate is called
    //   Then the "unknown" canonical id is returned
    //   And no row is created for the rejected token itself
    //   And NewRowsCreated is 1 (the "unknown" row, not the rejected token)

    [Fact]
    public async Task GetOrCreate_RejectedToken_ReturnsUnknownIdAndDoesNotCreateSeparateRow()
    {
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        // "and" normalizes to a StopwordsOnly rejection; GetOrCreate falls back to "unknown".
        var unknownId = registry.GetOrCreate("and");

        // Assert
        unknownId.Should().NotBe(Guid.Empty);
        registry.NewRowsCreated.Should().Be(1);
        _dbContext.CanonicalIngredients.Local
            .Should().ContainSingle(c => c.Name == "unknown");
        _dbContext.CanonicalIngredients.Local
            .Should().NotContain(c => c.Name == "and");
    }

    // Scenario: Two distinct rejected tokens both return the same "unknown" id
    //   Given two calls to GetOrCreate with different rejected tokens
    //   When both calls complete
    //   Then both return the same "unknown" id
    //   And NewRowsCreated remains 1

    [Fact]
    public async Task GetOrCreate_TwoRejectedTokens_BothReturnSameUnknownId()
    {
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        // "and" → StopwordsOnly, "1/2" → NoLetters; both fall back to "unknown".
        var firstId = registry.GetOrCreate("and");
        var secondId = registry.GetOrCreate("1/2");

        // Assert
        firstId.Should().Be(secondId);
        registry.NewRowsCreated.Should().Be(1);
    }

    // Scenario: Whitespace-padded and clean token resolve to the same canonical row
    //   Given no existing CanonicalIngredient for "Apple"
    //   When GetOrCreate is called with "  Apple  " and then "apple"
    //   Then both return the same id (normalization + case-insensitive deduplication)
    //   And exactly one new row is created

    [Fact]
    public async Task GetOrCreate_WhitespacePaddedAndCleanToken_ResolveToCaseInsensitiveSameRow()
    {
        var registry = await CanonicalIngredientRegistry.LoadAsync(_dbContext);

        // "  Apple  " normalizes to "Apple"; the registry deduplicates case-insensitively,
        // so "apple" on the second call resolves to the same row.
        var paddedId = registry.GetOrCreate("  Apple  ");
        var cleanId = registry.GetOrCreate("apple");

        // Assert
        paddedId.Should().Be(cleanId);
        registry.NewRowsCreated.Should().Be(1);
    }
}
