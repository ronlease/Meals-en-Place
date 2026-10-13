// Feature: InMemoryUnitOfMeasureResolver
//
// Scenario: Abbreviation match resolves without queueing
//   Given seeded units of measure including "cup"
//   When NormalizeOrDefer("2 cups", ...) is called
//   Then the result resolves to Cup with quantity 2
//   And WasDeferred is false
//   And no UnresolvedUnitOfMeasureToken row is written to the DbContext
//
// Scenario: Alias match resolves without queueing
//   Given a "c." alias pointing to Cup
//   When NormalizeOrDefer("1 c.", ...) is called
//   Then the result resolves to Cup with quantity 1
//
// Scenario: Count-noun fallback resolves to Each
//   Given quantity > 0 but no unit / alias match
//   When NormalizeOrDefer is called
//   Then the result resolves to Each without queueing
//
// Scenario: Unresolved token with no quantity writes a new queue row
//   Given measure "a smidge" (quantity 0, token "a smidge")
//   When NormalizeOrDefer is called
//   Then WasDeferred is true
//   And a new UnresolvedUnitOfMeasureToken row is added to the DbContext with Count = 1
//
// Scenario: Repeat occurrence of the same unresolved token increments an existing row
//   Given the same "a smidge" called twice
//   When NormalizeOrDefer is called twice in the same batch
//   Then a single row exists with Count = 2
//   And SampleIngredientContext reflects the latest caller
//
// Scenario: Empty unit token does not write a queue row
//   Given a measure string with no remainder (e.g. "5")
//   When NormalizeOrDefer is called
//   Then no queue row is written
//
// Scenario: ResetPerBatchState clears the per-batch dedup map
//   Given NormalizeOrDefer was called in batch 1, ResetPerBatchState cleared, then ChangeTracker.Clear, then a new instance
//   When NormalizeOrDefer is called again with the same unresolved token
//   Then the resolver re-queries the DB for the existing row and increments it
//
// Scenario: Over-length inputs are truncated at column caps
//   Given an unresolved unit token longer than 100 chars and a measure / ingredient context longer than 500 chars
//   When NormalizeOrDefer is called
//   Then the queue row is written with UnitToken truncated to 100 chars and sample columns truncated to 500 chars
//
// MEP-063: Count fallback must not swallow unrecognized unit tokens
//
// Scenario: Unrecognized unit token defers with the leading word as the token (AC1)
//   Given measure "2 sprigs thyme" where "sprigs" matches no unit, name, or alias
//   When NormalizeOrDefer is called
//   Then WasDeferred is true and DeferredUnitToken is "sprigs"
//   And the queue row UnitToken is "sprigs" with the full measure string as the sample
//
// Scenario: Abbreviation followed by an ingredient resolves with its type
//   Given measure "1 c. flour"
//   When NormalizeOrDefer is called
//   Then the result is 1 cup of type Volume
//
// Scenario: Size descriptor before the ingredient noun resolves to each
//   Given measure "2 large eggs" for ingredient "egg"
//   When NormalizeOrDefer is called
//   Then the result is 2 ea of type Count
//
// Scenario: Mixed number resolves the summed quantity
//   Given measure "1 1/2 cups sugar"
//   When NormalizeOrDefer is called
//   Then the quantity is 1.5 cup
//
// Scenario: Resolved result carries the unit of measure type
//   Given measures "2 lbs ground beef", "1 tbsp" and "3 eggs"
//   When NormalizeOrDefer is called
//   Then the types are Weight, Volume and Count respectively
//
// Scenario: Bare quantity resolves to each (AC2)
//   Given measure "5" for ingredient "eggs"
//   When NormalizeOrDefer is called
//   Then the result is 5 ea, WasDeferred is false, and no queue row is written

//
// MEP-067: count/measure words, junk, ranges, to-taste, butter sticks
//
// Scenario: Count words resolve to each without queueing
//   Given seeded aliases clove(s), slice(s), bunch(es), stick(s)
//   When NormalizeOrDefer("2 cloves garlic" etc.) is called
//   Then the result is ea and no queue row is written
//
// Scenario: Pinch and dash resolve to their seeded units
//   Given seeded Pinch and Dash units with aliases pinches and dashes
//   When NormalizeOrDefer("1 pinch salt" / "2 dashes hot sauce") is called
//   Then the result resolves to pinch / dash without queueing
//
// Scenario: Butter sticks resolve to grams
//   Given "2 sticks butter"
//   Then the result is 226 g; and "1 stick cinnamon" is 1 ea
//
// Scenario: Junk lines are dropped without queueing
//   Given "None", "-", "to" or "or"
//   Then IsJunkLine is true, WasDeferred is false and no queue row is written
//
// Scenario: Ranges, to-taste lines and size descriptors never queue
//   Given "1-1/2 cups", "2-3 tbsp", "salt and pepper to taste", "2 large cloves garlic"
//   Then each resolves (1.5 cup, 2 tbsp, to-taste, 2 ea) with no queue row

using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using MealsEnPlace.Tools.Ingest;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class InMemoryUnitOfMeasureResolverTests : IDisposable
{
    private readonly MealsEnPlaceDbContext _dbContext;

    public InMemoryUnitOfMeasureResolverTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);

        // Seed a subset of units of measure sufficient for the test scenarios.
        _dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.EachId,
                Name = "Each",
                UnitOfMeasureType = UnitOfMeasureType.Count
            },
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
                Abbreviation = "g",
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.GramId,
                Name = "Gram",
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
                Abbreviation = "ml",
                ConversionFactor = 1.0m,
                Id = UnitOfMeasureConfiguration.MlId,
                Name = "Milliliter",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "dash",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 0.616m,
                Id = UnitOfMeasureConfiguration.DashId,
                Name = "Dash",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "pinch",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.MlId,
                ConversionFactor = 0.308m,
                Id = UnitOfMeasureConfiguration.PinchId,
                Name = "Pinch",
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
            });

        _dbContext.UnitOfMeasureAliases.AddRange(
            new UnitOfMeasureAlias
            {
                Alias = "c.",
                CreatedAt = DateTime.UtcNow,
                Id = Guid.NewGuid(),
                UnitOfMeasureId = UnitOfMeasureConfiguration.CupId
            },
            NewAlias("clove", UnitOfMeasureConfiguration.EachId),
            NewAlias("cloves", UnitOfMeasureConfiguration.EachId),
            NewAlias("slice", UnitOfMeasureConfiguration.EachId),
            NewAlias("slices", UnitOfMeasureConfiguration.EachId),
            NewAlias("bunch", UnitOfMeasureConfiguration.EachId),
            NewAlias("bunches", UnitOfMeasureConfiguration.EachId),
            NewAlias("stick", UnitOfMeasureConfiguration.EachId),
            NewAlias("sticks", UnitOfMeasureConfiguration.EachId),
            NewAlias("pinches", UnitOfMeasureConfiguration.PinchId),
            NewAlias("dashes", UnitOfMeasureConfiguration.DashId),
            new UnitOfMeasureAlias
            {
                Alias = "lbs",
                CreatedAt = DateTime.UtcNow,
                Id = Guid.NewGuid(),
                UnitOfMeasureId = UnitOfMeasureConfiguration.LbId
            });

        _dbContext.SaveChanges();
    }

    public void Dispose() => _dbContext.Dispose();

    private static UnitOfMeasureAlias NewAlias(string alias, Guid unitOfMeasureId) =>
        new()
        {
            Alias = alias,
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            UnitOfMeasureId = unitOfMeasureId
        };

    // ── Deterministic resolution paths ────────────────────────────────────────

    [Fact]
    public async Task NormalizeOrDefer_AbbreviationMatch_ResolvesWithoutQueueing()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("2 cups", "flour");

        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be(2m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.CupId);
        result.UnitOfMeasureAbbreviation.Should().Be("cup");

        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task NormalizeOrDefer_AliasMatch_ResolvesWithoutQueueing()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("1 c.", "flour");

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.CupId);
    }

    [Fact]
    public async Task NormalizeOrDefer_CountNounFallback_ResolvesToEach()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("4 chicken breasts", "chicken");

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.Quantity.Should().Be(4m);
    }

    // ── Deferral paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task NormalizeOrDefer_UnresolvedToken_WritesNewQueueRow()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("a smidge", "cayenne");

        result.WasDeferred.Should().BeTrue();
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().HaveCount(1);

        var queueRow = _dbContext.UnresolvedUnitOfMeasureTokens.Local.First();
        queueRow.UnitToken.Should().Be("a smidge");
        queueRow.Count.Should().Be(1);
        queueRow.SampleIngredientContext.Should().Be("cayenne");
        queueRow.SampleMeasureString.Should().Be("a smidge");
    }

    [Fact]
    public async Task NormalizeOrDefer_RepeatOccurrenceInSameBatch_IncrementsExistingRow()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        resolver.NormalizeOrDefer("a smidge", "cayenne");
        resolver.NormalizeOrDefer("a smidge", "pepper");

        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().HaveCount(1);

        var queueRow = _dbContext.UnresolvedUnitOfMeasureTokens.Local.First();
        queueRow.Count.Should().Be(2);
        queueRow.SampleIngredientContext.Should().Be("pepper");
    }

    [Fact]
    public async Task NormalizeOrDefer_BareQuantity_ResolvesToEach()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("5", "eggs");

        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be(5m);
        result.UnitOfMeasureAbbreviation.Should().Be("ea");
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task NormalizeOrDefer_UnrecognizedUnitToken_DefersWithLeadingWordToken()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("2 sprigs thyme", "thyme");

        result.WasDeferred.Should().BeTrue();
        result.UnitOfMeasureId.Should().BeNull();
        result.DeferredUnitToken.Should().Be("sprigs");

        var queueRow = _dbContext.UnresolvedUnitOfMeasureTokens.Local.Single();
        queueRow.UnitToken.Should().Be("sprigs");
        queueRow.SampleMeasureString.Should().Be("2 sprigs thyme");
        queueRow.SampleIngredientContext.Should().Be("thyme");
    }

    [Fact]
    public async Task NormalizeOrDefer_AbbreviationFollowedByIngredient_ResolvesWithType()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("1 c. flour", "flour");

        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be(1m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.CupId);
        result.UnitOfMeasureType.Should().Be(UnitOfMeasureType.Volume);
    }

    [Fact]
    public async Task NormalizeOrDefer_SizeDescriptor_ResolvesToEach()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("2 large eggs", "egg");

        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be(2m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.UnitOfMeasureType.Should().Be(UnitOfMeasureType.Count);
    }

    [Fact]
    public async Task NormalizeOrDefer_MixedNumber_ResolvesQuantity()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer("1 1/2 cups sugar", "sugar");

        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be(1.5m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.CupId);
    }

    [Theory]
    [InlineData("2 lbs ground beef", "ground beef", UnitOfMeasureType.Weight)]
    [InlineData("1 tbsp", "butter", UnitOfMeasureType.Volume)]
    [InlineData("3 eggs", "egg", UnitOfMeasureType.Count)]
    public async Task NormalizeOrDefer_ResolvedResult_CarriesUnitOfMeasureType(
        string measureString,
        string ingredientName,
        UnitOfMeasureType expectedType)
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer(measureString, ingredientName);

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureType.Should().Be(expectedType);
    }

    [Fact]
    public async Task NormalizeOrDefer_OverLengthInputs_TruncatesAtColumnCaps()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        // UnitToken column is varchar(100); sample columns are varchar(500).
        // No leading number, so the parser returns quantity 0 and the full
        // string as the unit token, which bypasses the count-noun fallback
        // and reaches the queue write path.
        var longMeasure = new string('u', 700);
        var longIngredient = new string('i', 650);

        var result = resolver.NormalizeOrDefer(longMeasure, longIngredient);

        result.WasDeferred.Should().BeTrue();
        var row = _dbContext.UnresolvedUnitOfMeasureTokens.Local.Single();
        row.UnitToken.Length.Should().Be(100);
        row.SampleMeasureString.Length.Should().Be(500);
        row.SampleIngredientContext.Length.Should().Be(500);
    }

    [Fact]
    public async Task NormalizeOrDefer_RepeatOverLengthToken_IncrementsSameRow()
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        // Two distinct strings that truncate to the same 100-char prefix.
        var sharedPrefix = new string('u', 100);
        var first = sharedPrefix + "-alpha";
        var second = sharedPrefix + "-beta";

        resolver.NormalizeOrDefer(first, "ctx1");
        resolver.NormalizeOrDefer(second, "ctx2");

        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().HaveCount(1);
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Single().Count.Should().Be(2);
    }

    // ── MEP-067: count words, pinch/dash, sticks, junk, ranges, to-taste ──────

    [Theory]
    [InlineData("2 cloves garlic", "garlic", 2)]
    [InlineData("1 clove garlic", "garlic", 1)]
    [InlineData("3 slices bacon", "bacon", 3)]
    [InlineData("1 slice bread", "bread", 1)]
    [InlineData("2 bunches kale", "kale", 2)]
    [InlineData("1 bunch parsley", "parsley", 1)]
    [InlineData("2 large cloves garlic", "garlic", 2)]
    [InlineData("1 stick cinnamon", "cinnamon", 1)]
    public async Task NormalizeOrDefer_CountWord_ResolvesToEachWithoutQueueing(
        string measureString, string ingredientName, int expectedQuantity)
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer(measureString, ingredientName);

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.Quantity.Should().Be(expectedQuantity);
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1 pinch salt", "salt", 1, "pinch")]
    [InlineData("2 pinches salt", "salt", 2, "pinch")]
    [InlineData("1 dash hot sauce", "hot sauce", 1, "dash")]
    [InlineData("2 dashes hot sauce", "hot sauce", 2, "dash")]
    public async Task NormalizeOrDefer_PinchOrDash_ResolvesToSeededUnit(
        string measureString, string ingredientName, int expectedQuantity, string expectedAbbreviation)
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer(measureString, ingredientName);

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureAbbreviation.Should().Be(expectedAbbreviation);
        result.UnitOfMeasureType.Should().Be(UnitOfMeasureType.Volume);
        result.Quantity.Should().Be(expectedQuantity);
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Theory]
    [InlineData("2 sticks butter", "butter", 226)]
    [InlineData("1 stick butter", "butter", 113)]
    [InlineData("1 stick margarine", "margarine", 113)]
    public async Task NormalizeOrDefer_ButterStick_ResolvesToGrams(
        string measureString, string ingredientName, int expectedGrams)
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer(measureString, ingredientName);

        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.GramId);
        result.UnitOfMeasureAbbreviation.Should().Be("g");
        result.UnitOfMeasureType.Should().Be(UnitOfMeasureType.Weight);
        result.Quantity.Should().Be(expectedGrams);
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Theory]
    [InlineData("None")]
    [InlineData("-")]
    [InlineData("to")]
    [InlineData("or")]
    public async Task NormalizeOrDefer_JunkLine_ReturnsJunkLineWithoutQueueing(string measureString)
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer(measureString, "salt");

        result.IsJunkLine.Should().BeTrue();
        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().BeNull();
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1-1/2 cups sugar", "sugar", 1.5, "cup")]
    [InlineData("2-3 tbsp butter", "butter", 2, "tbsp")]
    public async Task NormalizeOrDefer_Range_ResolvesLowerBoundWithoutQueueing(
        string measureString, string ingredientName, double expectedQuantity, string expectedAbbreviation)
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer(measureString, ingredientName);

        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be((decimal)expectedQuantity);
        result.UnitOfMeasureAbbreviation.Should().Be(expectedAbbreviation);
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Theory]
    [InlineData("salt to taste", "salt")]
    [InlineData("salt and pepper", "salt")]
    [InlineData("salt and pepper to taste", "salt")]
    [InlineData("pepper", "pepper")]
    public async Task NormalizeOrDefer_ToTasteLine_ResolvesToZeroEachWithoutQueueing(
        string measureString, string ingredientName)
    {
        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);

        var result = resolver.NormalizeOrDefer(measureString, ingredientName);

        result.IsToTaste.Should().BeTrue();
        result.WasDeferred.Should().BeFalse();
        result.Quantity.Should().Be(0m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.UnitOfMeasureAbbreviation.Should().Be("ea");
        _dbContext.UnresolvedUnitOfMeasureTokens.Local.Should().BeEmpty();
    }

    [Fact]
    public void JunkLine_Factory_HasNoUnitAndIsNotDeferred()
    {
        var result = IngestUnitOfMeasureResolution.JunkLine();

        result.IsJunkLine.Should().BeTrue();
        result.IsToTaste.Should().BeFalse();
        result.WasDeferred.Should().BeFalse();
        result.UnitOfMeasureId.Should().BeNull();
        result.DeferredUnitToken.Should().BeNull();
    }

    // ── Per-batch state reset ─────────────────────────────────────────────────

    [Fact]
    public async Task ResetPerBatchState_AfterChangeTrackerClear_ReQueriesDbForExistingRow()
    {
        // Pre-seed the DB with an existing queue row (as if a prior batch wrote it).
        _dbContext.UnresolvedUnitOfMeasureTokens.Add(new UnresolvedUnitOfMeasureToken
        {
            Count = 1,
            FirstSeenAt = DateTime.UtcNow.AddHours(-1),
            Id = Guid.NewGuid(),
            LastSeenAt = DateTime.UtcNow.AddHours(-1),
            SampleIngredientContext = "(old)",
            SampleMeasureString = "a smidge",
            UnitToken = "a smidge"
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var resolver = await InMemoryUnitOfMeasureResolver.LoadAsync(_dbContext);
        resolver.ResetPerBatchState();

        // New batch: same token arrives
        resolver.NormalizeOrDefer("a smidge", "fresh cayenne");

        var rows = await _dbContext.UnresolvedUnitOfMeasureTokens.ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Count.Should().Be(2);
        rows[0].SampleIngredientContext.Should().Be("fresh cayenne");
    }
}
