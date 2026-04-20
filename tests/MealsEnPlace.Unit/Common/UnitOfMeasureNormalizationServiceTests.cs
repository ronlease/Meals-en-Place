// Feature: unit of measure Normalization — Normalization Service
//
// Scenario: Standard unit is resolved deterministically and Claude is not invoked
//   Given the measure string "8 oz"
//   And "oz" exists in the unit of measure table
//   When NormalizeAsync is called
//   Then the result Quantity is 8
//   And UnitOfMeasureAbbreviation is "oz"
//   And WasClaudeResolved is false
//   And Confidence is High
//   And Claude is never called
//
// Scenario: Standard volume unit is resolved deterministically
//   Given the measure string "2 cups"
//   And "cup" exists in the unit of measure table
//   When NormalizeAsync is called
//   Then the result Quantity is 2
//   And UnitOfMeasureAbbreviation is "cup"
//   And WasClaudeResolved is false
//   And Confidence is High
//   And Claude is never called
//
// Scenario: Fraction measure string is parsed and resolved deterministically
//   Given the measure string "1/2 cup"
//   And "cup" exists in the unit of measure table
//   When NormalizeAsync is called
//   Then the result Quantity is 0.5
//   And UnitOfMeasureAbbreviation is "cup"
//   And WasClaudeResolved is false
//   And Confidence is High
//   And Claude is never called
//
// Scenario: Quarter fraction is parsed correctly
//   Given the measure string "1/4 tsp"
//   And "tsp" exists in the unit of measure table
//   When NormalizeAsync is called
//   Then the result Quantity is 0.25
//   And WasClaudeResolved is false
//
// Scenario: Colloquial unit falls back to Claude
//   Given the measure string "a knob" for ingredient "butter"
//   And "knob" does not exist in the unit of measure table
//   When NormalizeAsync is called
//   Then Claude is invoked via ResolveUnitOfMeasureAsync
//   And WasClaudeResolved is true
//   And the result reflects the Claude-returned quantity and unit
//
// Scenario: WasClaudeResolved is true on any Claude-backed result
//   Given a measure string whose unit token is not in the unit of measure table
//   When NormalizeAsync is called
//   Then WasClaudeResolved is true regardless of Confidence
//
// Scenario: Claude returns a known unit of measure abbreviation — UnitOfMeasureId is populated
//   Given Claude resolves a colloquial string to "g"
//   And "g" exists in the unit of measure table
//   When NormalizeAsync is called
//   Then UnitOfMeasureId equals the seeded Gram ID
//   And UnitOfMeasureAbbreviation is "g"
//
// Scenario: Claude returns an unknown abbreviation — UnitOfMeasureId is Guid.Empty
//   Given Claude resolves a colloquial string to an unmapped abbreviation "splash"
//   And "splash" does not exist in the unit of measure table
//   When NormalizeAsync is called
//   Then UnitOfMeasureId is Guid.Empty
//   And UnitOfMeasureAbbreviation is "splash"
//
// Scenario: Unknown unit with no Claude match returns Low confidence
//   Given a measure string with an unmapped unit token
//   And Claude returns Confidence.Low with an empty ResolvedUnitOfMeasure
//   When NormalizeAsync is called
//   Then Confidence is Low
//   And WasClaudeResolved is true
//   And UnitOfMeasureId is Guid.Empty
//
// Scenario: Unit token match is case-insensitive
//   Given the measure string "500G" (uppercase G)
//   And "g" exists in the unit of measure table with lowercase abbreviation
//   When NormalizeAsync is called
//   Then the unit is resolved deterministically
//   And WasClaudeResolved is false
//
// Scenario: WasClaudeResolved is false for all deterministic resolutions
//   Given the measure string "1 tbsp"
//   And "tbsp" exists in the unit of measure table
//   When NormalizeAsync is called
//   Then WasClaudeResolved is false
//   And Confidence is High
//
// Scenario: Claude Notes are surfaced on Claude-resolved results
//   Given Claude returns Notes "Assumed standard knob size"
//   When NormalizeAsync is called for "a knob" of butter
//   Then the Notes field on NormalizationResult equals "Assumed standard knob size"
//
// Scenario: Notes is null on a deterministic resolution
//   Given the measure string "2 tbsp"
//   When NormalizeAsync is called
//   Then Notes is null
//
// Scenario: Claude is invoked exactly once per NormalizeAsync call for colloquial units
//   Given a colloquial measure string
//   When NormalizeAsync is called once
//   Then ResolveUnitOfMeasureAsync is called exactly once on the mock

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MealsEnPlace.Unit.Common;

public class UnitOfMeasureNormalizationServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MealsEnPlaceDbContext CreateSeededDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var dbContext = new MealsEnPlaceDbContext(options);

        // Seed a representative subset of unit of measure rows sufficient for normalization tests.
        // Base units first (no foreign-key dependency).
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
                Abbreviation = "oz",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 28.350m,
                Id = UnitOfMeasureConfiguration.OzId,
                Name = "Ounce",
                UnitOfMeasureType = UnitOfMeasureType.Weight
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
            new UnitOfMeasure
            {
                Abbreviation = "lb",
                BaseUnitOfMeasureId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 453.592m,
                Id = UnitOfMeasureConfiguration.LbId,
                Name = "Pound",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            }
        );

        // Seed a representative subset of aliases for the alias-lookup tests.
        var seededAt = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);
        dbContext.UnitOfMeasureAliases.AddRange(
            new UnitOfMeasureAlias
            {
                Alias = "c.",
                CreatedAt = seededAt,
                Id = new Guid("a2000000-0000-0000-0000-000000000002"),
                UnitOfMeasureId = UnitOfMeasureConfiguration.CupId
            },
            new UnitOfMeasureAlias
            {
                Alias = "Tbsp.",
                CreatedAt = seededAt,
                Id = new Guid("a2000000-0000-0000-0000-000000000015"),
                UnitOfMeasureId = UnitOfMeasureConfiguration.TbspId
            },
            new UnitOfMeasureAlias
            {
                Alias = "lbs",
                CreatedAt = seededAt,
                Id = new Guid("a2000000-0000-0000-0000-000000000051"),
                UnitOfMeasureId = UnitOfMeasureConfiguration.LbId
            }
        );

        dbContext.SaveChanges();
        return dbContext;
    }

    private static IClaudeAvailability CreateAvailability(bool isConfigured = true)
    {
        var mock = new Mock<IClaudeAvailability>(MockBehavior.Loose);
        mock.Setup(a => a.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isConfigured);
        return mock.Object;
    }

    private static Mock<IClaudeService> CreateStrictClaudeMock() =>
        new(MockBehavior.Strict);

    private static UnitOfMeasureNormalizationService BuildService(
        MealsEnPlaceDbContext dbContext,
        IClaudeService claudeService,
        bool claudeConfigured = true) =>
        new(CreateAvailability(claudeConfigured), claudeService, dbContext);

    // ── Standard unit — deterministic path, Claude never called ──────────────

    [Fact]
    public async Task NormalizeAsync_StandardUnit8Oz_ReturnsQuantity8AndOzAbbreviationWithoutClaude()
    {
        // Arrange — "oz" is seeded; Claude must never be invoked for deterministic units
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_StandardUnit8Oz_ReturnsQuantity8AndOzAbbreviationWithoutClaude));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("8 oz", "cheddar cheese");

        // Assert
        result.Quantity.Should().Be(8m);
        result.UnitOfMeasureAbbreviation.Should().Be("oz");
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.OzId);
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
        // Strict mock verifies Claude was never called — any call would throw
    }

    [Fact]
    public async Task NormalizeAsync_StandardUnitCup_ReturnsQuantityAndAbbreviationWithoutClaude()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_StandardUnitCup_ReturnsQuantityAndAbbreviationWithoutClaude));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("2 cups", "flour");

        // Assert
        result.Quantity.Should().Be(2m);
        result.UnitOfMeasureAbbreviation.Should().Be("cup");
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
    }

    [Fact]
    public async Task NormalizeAsync_StandardUnitTbsp_WasClaudeResolvedIsFalse()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_StandardUnitTbsp_WasClaudeResolvedIsFalse));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("1 tbsp", "olive oil");

        // Assert
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
    }

    // ── Standard unit — Notes is null on deterministic resolution ────────────

    [Fact]
    public async Task NormalizeAsync_DeterministicResolution_NotesIsNull()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_DeterministicResolution_NotesIsNull));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("2 tbsp", "vinegar");

        // Assert
        result.Notes.Should().BeNull();
    }

    // ── Fraction parsing ──────────────────────────────────────────────────────

    [Fact]
    public async Task NormalizeAsync_FractionHalfCup_ParsesQuantityAsPointFive()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_FractionHalfCup_ParsesQuantityAsPointFive));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("1/2 cup", "milk");

        // Assert
        result.Quantity.Should().Be(0.5m);
        result.UnitOfMeasureAbbreviation.Should().Be("cup");
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
    }

    [Fact]
    public async Task NormalizeAsync_FractionQuarterTsp_ParsesQuantityAsPointTwoFive()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_FractionQuarterTsp_ParsesQuantityAsPointTwoFive));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("1/4 tsp", "salt");

        // Assert
        result.Quantity.Should().Be(0.25m);
        result.WasClaudeResolved.Should().BeFalse();
    }

    // ── Case-insensitive unit token matching ──────────────────────────────────

    [Fact]
    public async Task NormalizeAsync_UppercaseUnitToken_ResolvesDeterministically()
    {
        // Arrange — measure string uses uppercase "G"; abbreviation in DB is lowercase "g"
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_UppercaseUnitToken_ResolvesDeterministically));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("500G", "flour");

        // Assert
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
        result.UnitOfMeasureAbbreviation.Should().Be("g");
    }

    // ── Colloquial unit — Claude fallback path ────────────────────────────────

    [Fact]
    public async Task NormalizeAsync_ColloquialKnobOfButter_InvokesClaudeAndReturnsClaudeResult()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ColloquialKnobOfButter_InvokesClaudeAndReturnsClaudeResult));
        var claudeMock = new Mock<IClaudeService>(MockBehavior.Strict);
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync("a knob", "butter"))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.High,
                Notes = "Assumed standard knob size; user may override.",
                ResolvedQuantity = 15m,
                ResolvedUnitOfMeasure = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a knob", "butter");

        // Assert
        result.WasClaudeResolved.Should().BeTrue();
        result.Quantity.Should().Be(15m);
        result.UnitOfMeasureAbbreviation.Should().Be("g");
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.GramId);
        result.Confidence.Should().Be(ClaudeConfidence.High);
        claudeMock.Verify(c => c.ResolveUnitOfMeasureAsync("a knob", "butter"), Times.Once);
    }

    [Fact]
    public async Task NormalizeAsync_ColloquialUnit_WasClaudeResolvedIsTrue()
    {
        // Arrange — any unmapped unit token triggers the Claude path
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ColloquialUnit_WasClaudeResolvedIsTrue));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.Medium,
                Notes = string.Empty,
                ResolvedQuantity = 10m,
                ResolvedUnitOfMeasure = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a pinch", "cayenne");

        // Assert
        result.WasClaudeResolved.Should().BeTrue();
    }

    [Fact]
    public async Task NormalizeAsync_ClaudeResolvesToKnownAbbreviation_UnitOfMeasureIdIsPopulated()
    {
        // Arrange — Claude returns "g", which is seeded in the database.
        // Use a no-quantity measure string so the MEP-026 count-with-ingredient-noun
        // fallback does not short-circuit this Claude path.
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ClaudeResolvesToKnownAbbreviation_UnitOfMeasureIdIsPopulated));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.High,
                Notes = string.Empty,
                ResolvedQuantity = 15m,
                ResolvedUnitOfMeasure = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a head", "garlic");

        // Assert
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.GramId);
        result.UnitOfMeasureAbbreviation.Should().Be("g");
        result.WasClaudeResolved.Should().BeTrue();
    }

    [Fact]
    public async Task NormalizeAsync_ClaudeResolvesToUnknownAbbreviation_UnitOfMeasureIdIsEmpty()
    {
        // Arrange — Claude returns "splash", which is not in the database
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ClaudeResolvesToUnknownAbbreviation_UnitOfMeasureIdIsEmpty));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.Low,
                Notes = "Could not map to a canonical unit.",
                ResolvedQuantity = 0m,
                ResolvedUnitOfMeasure = "splash"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a splash", "vinegar");

        // Assert
        result.UnitOfMeasureId.Should().Be(Guid.Empty);
        result.UnitOfMeasureAbbreviation.Should().Be("splash");
        result.WasClaudeResolved.Should().BeTrue();
    }

    // ── Low confidence — unknown unit with no Claude match ────────────────────

    [Fact]
    public async Task NormalizeAsync_UnknownUnitWithLowConfidenceClaudeResult_ReturnsLowConfidence()
    {
        // Arrange — Claude cannot resolve; returns Low confidence and empty ResolvedUnitOfMeasure.
        // Use a no-quantity measure string so the MEP-026 count-with-ingredient-noun
        // fallback does not short-circuit this Claude path.
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_UnknownUnitWithLowConfidenceClaudeResult_ReturnsLowConfidence));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.Low,
                Notes = "Claude integration not yet configured. Please declare the quantity and unit manually.",
                ResolvedQuantity = 0m,
                ResolvedUnitOfMeasure = string.Empty
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a schmear", "cream cheese");

        // Assert
        result.Confidence.Should().Be(ClaudeConfidence.Low);
        result.WasClaudeResolved.Should().BeTrue();
        result.UnitOfMeasureId.Should().Be(Guid.Empty);
    }

    // ── Claude Notes surfaced on Claude-resolved results ──────────────────────

    [Fact]
    public async Task NormalizeAsync_ClaudeResolution_NotesFieldMatchesClaudeNotes()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ClaudeResolution_NotesFieldMatchesClaudeNotes));
        const string expectedNotes = "Assumed standard knob size; user may override.";
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.High,
                Notes = expectedNotes,
                ResolvedQuantity = 15m,
                ResolvedUnitOfMeasure = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a knob", "butter");

        // Assert
        result.Notes.Should().Be(expectedNotes);
    }

    // ── Claude invocation count ───────────────────────────────────────────────

    [Fact]
    public async Task NormalizeAsync_ColloquialUnit_InvokesClaudeExactlyOnce()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ColloquialUnit_InvokesClaudeExactlyOnce));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.Medium,
                Notes = string.Empty,
                ResolvedQuantity = 5m,
                ResolvedUnitOfMeasure = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        await service.NormalizeAsync("a sprinkle", "paprika");

        // Assert — exactly one Claude call, not zero and not more than one
        claudeMock.Verify(
            c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    // ── MEP-026: Alias-table lookup ───────────────────────────────────────────
    //
    // Scenario: Dotted-abbreviation alias resolves deterministically
    //   Given the measure string "1 c. flour"
    //   And an alias row "c." maps to the Cup UnitOfMeasure
    //   When NormalizeAsync is called
    //   Then the result resolves to the Cup unit of measure with quantity 1
    //   And WasClaudeResolved is false
    //   And Claude is never called

    [Fact]
    public async Task NormalizeAsync_DottedCupAlias_ResolvesToCupWithoutClaude()
    {
        // Arrange — dotted abbreviation "c." is seeded as an alias for Cup
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_DottedCupAlias_ResolvesToCupWithoutClaude));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act — "1 c." splits into quantity 1 and unit token "c."
        var result = await service.NormalizeAsync("1 c.", "flour");

        // Assert
        result.Quantity.Should().Be(1m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.CupId);
        result.UnitOfMeasureAbbreviation.Should().Be("cup");
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
    }

    // Scenario: Plural-form alias resolves deterministically
    //   Given the measure string "2 lbs chicken"
    //   And an alias row "lbs" maps to the Pound UnitOfMeasure
    //   When NormalizeAsync is called
    //   Then the result resolves to the Pound unit of measure with quantity 2
    //   And WasClaudeResolved is false

    [Fact]
    public async Task NormalizeAsync_PluralLbsAlias_ResolvesToPoundWithoutClaude()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_PluralLbsAlias_ResolvesToPoundWithoutClaude));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("2 lbs", "ground beef");

        // Assert
        result.Quantity.Should().Be(2m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.LbId);
        result.UnitOfMeasureAbbreviation.Should().Be("lb");
        result.WasClaudeResolved.Should().BeFalse();
    }

    // Scenario: Alias lookup is case-insensitive
    //   Given the measure string "3 TBSP." with the "Tbsp." alias seeded
    //   When NormalizeAsync is called
    //   Then the alias matches regardless of case

    [Fact]
    public async Task NormalizeAsync_AliasLookupIsCaseInsensitive()
    {
        // Arrange — seeded alias is "Tbsp.", input uses "TBSP."
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_AliasLookupIsCaseInsensitive));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("3 TBSP.", "olive oil");

        // Assert
        result.Quantity.Should().Be(3m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.TbspId);
        result.WasClaudeResolved.Should().BeFalse();
    }

    // Scenario: Abbreviation match takes precedence over alias match
    //   Given both "tbsp" (abbreviation) and "Tbsp." (alias) exist
    //   When the measure string is "1 tbsp"
    //   Then the result is resolved via the abbreviation lookup, not the alias
    //   And WasClaudeResolved is false

    [Fact]
    public async Task NormalizeAsync_AbbreviationMatchTakesPrecedenceOverAlias()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_AbbreviationMatchTakesPrecedenceOverAlias));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("1 tbsp", "butter");

        // Assert — resolved via Step 1 (abbreviation), alias step never touched
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.TbspId);
        result.WasClaudeResolved.Should().BeFalse();
    }

    // ── MEP-026: Count-with-ingredient-noun fallback ──────────────────────────
    //
    // Scenario: Count-with-ingredient-noun defaults to "each"
    //   Given the measure string "4 chicken breasts"
    //   And the quantity parses as 4 but "chicken breasts" is not a unit of measure or alias
    //   When NormalizeAsync is called
    //   Then the result resolves to the Each unit of measure with quantity 4
    //   And WasClaudeResolved is false
    //   And Claude is never called

    [Fact]
    public async Task NormalizeAsync_CountWithIngredientNoun_DefaultsToEachWithoutClaude()
    {
        // Arrange — "chicken breasts" is not a unit of measure, name, or alias
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_CountWithIngredientNoun_DefaultsToEachWithoutClaude));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("4 chicken breasts", "chicken");

        // Assert
        result.Quantity.Should().Be(4m);
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.UnitOfMeasureAbbreviation.Should().Be("ea");
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
    }

    // Scenario: Measure string with no numeric quantity does NOT fall back to "each"
    //   Given the measure string "a pinch of salt" (no leading number)
    //   When NormalizeAsync is called
    //   Then the count-fallback is NOT used
    //   And Claude is invoked as before

    [Fact]
    public async Task NormalizeAsync_NoNumericQuantity_FallsThroughToClaudeNotEach()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_NoNumericQuantity_FallsThroughToClaudeNotEach));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUnitOfMeasureAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UnitOfMeasureResolutionResult
            {
                Confidence = ClaudeConfidence.Medium,
                Notes = "Assumed pinch size",
                ResolvedQuantity = 1m,
                ResolvedUnitOfMeasure = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act — no leading digit, so quantity is 0 after parsing
        var result = await service.NormalizeAsync("a pinch", "salt");

        // Assert — Claude path, not the count-fallback
        result.WasClaudeResolved.Should().BeTrue();
        claudeMock.Verify(c => c.ResolveUnitOfMeasureAsync("a pinch", "salt"), Times.Once);
    }

    // ── MEP-026 Phase 2: NormalizeOrDeferAsync ───────────────────────────────
    //
    // Scenario: Deterministic match in ingest mode returns without touching the queue
    //   Given "2 cups" which resolves via direct abbreviation lookup
    //   When NormalizeOrDeferAsync is called
    //   Then the result resolves to Cup with quantity 2
    //   And WasDeferredToQueue is false
    //   And no UnresolvedUnitOfMeasureToken row is written
    //   And Claude is never called

    [Fact]
    public async Task NormalizeOrDeferAsync_DeterministicMatch_DoesNotWriteQueueRow()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeOrDeferAsync_DeterministicMatch_DoesNotWriteQueueRow));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeOrDeferAsync("2 cups", "flour");

        // Assert
        result.Quantity.Should().Be(2m);
        result.UnitOfMeasureAbbreviation.Should().Be("cup");
        result.WasDeferredToQueue.Should().BeFalse();
        result.WasClaudeResolved.Should().BeFalse();

        var queueCount = await dbContext.UnresolvedUnitOfMeasureTokens.CountAsync();
        queueCount.Should().Be(0);
    }

    // Scenario: Unresolved token writes a new queue row instead of invoking Claude
    //   Given "1 smidge" where "smidge" has no abbreviation, name, or alias match
    //   When NormalizeOrDeferAsync is called
    //   Then a new UnresolvedUnitOfMeasureToken row is written with Count=1 and the sample context
    //   And WasDeferredToQueue is true
    //   And Claude is never called

    [Fact]
    public async Task NormalizeOrDeferAsync_UnresolvedToken_WritesQueueRowAndDoesNotCallClaude()
    {
        // Arrange — "smidge" is not a unit of measure, not an alias, and count-fallback won't trigger
        // because the quantity-with-noun fallback does return "ea" for "1 smidge".
        // To reach the defer path, we need a measure string whose token doesn't match
        // anything AND whose quantity is zero. Use a no-quantity measure.
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeOrDeferAsync_UnresolvedToken_WritesQueueRowAndDoesNotCallClaude));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act — "a smidge" has no leading digit, so ParseMeasureString returns
        // (0, "a smidge"). No deterministic step matches.
        var result = await service.NormalizeOrDeferAsync("a smidge", "cayenne");

        // Assert
        result.WasDeferredToQueue.Should().BeTrue();
        result.WasClaudeResolved.Should().BeFalse();
        result.UnitOfMeasureId.Should().Be(Guid.Empty);

        var queueRows = await dbContext.UnresolvedUnitOfMeasureTokens.ToListAsync();
        queueRows.Should().HaveCount(1);
        queueRows[0].UnitToken.Should().Be("a smidge");
        queueRows[0].Count.Should().Be(1);
        queueRows[0].SampleMeasureString.Should().Be("a smidge");
        queueRows[0].SampleIngredientContext.Should().Be("cayenne");
    }

    // Scenario: Repeat occurrence of the same token increments Count rather than inserting a new row
    //   Given a queue row already exists for "smidge" with Count=1
    //   When NormalizeOrDeferAsync is called again with the same token
    //   Then the existing row's Count becomes 2
    //   And no new row is inserted
    //   And the LastSeenAt timestamp is refreshed

    [Fact]
    public async Task NormalizeOrDeferAsync_RepeatOccurrence_IncrementsCount()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeOrDeferAsync_RepeatOccurrence_IncrementsCount));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act — two calls with the same unresolved token
        await service.NormalizeOrDeferAsync("a smidge", "cayenne");
        await service.NormalizeOrDeferAsync("a smidge", "pepper flakes");

        // Assert
        var queueRows = await dbContext.UnresolvedUnitOfMeasureTokens.ToListAsync();
        queueRows.Should().HaveCount(1);
        queueRows[0].Count.Should().Be(2);
        queueRows[0].UnitToken.Should().Be("a smidge");
        // Most-recent sample context was refreshed
        queueRows[0].SampleIngredientContext.Should().Be("pepper flakes");
    }

    // Scenario: Count-with-ingredient-noun still wins in ingest mode
    //   Given "4 chicken breasts" which resolves via the count-fallback
    //   When NormalizeOrDeferAsync is called
    //   Then the result resolves to Each without writing a queue row

    [Fact]
    public async Task NormalizeOrDeferAsync_CountNounFallback_ResolvesAsEachNotDeferred()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeOrDeferAsync_CountNounFallback_ResolvesAsEachNotDeferred));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeOrDeferAsync("4 chicken breasts", "chicken");

        // Assert
        result.UnitOfMeasureId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.WasDeferredToQueue.Should().BeFalse();

        var queueCount = await dbContext.UnresolvedUnitOfMeasureTokens.CountAsync();
        queueCount.Should().Be(0);
    }

    // Scenario: Empty unit token (pure-numeric measure string) does not write a queue row
    //   Given "5" (a number with no unit token at all)
    //   When NormalizeOrDeferAsync is called
    //   Then no queue row is written

    [Fact]
    public async Task NormalizeOrDeferAsync_EmptyUnitToken_DoesNotWriteQueueRow()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeOrDeferAsync_EmptyUnitToken_DoesNotWriteQueueRow));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeOrDeferAsync("5", "eggs");

        // Assert — no token to queue
        var queueCount = await dbContext.UnresolvedUnitOfMeasureTokens.CountAsync();
        queueCount.Should().Be(0);
    }

    // ── MEP-032: graceful degradation without a Claude API key ───────────────
    //
    // Scenario: NormalizeAsync routes to the review queue when no Claude key is configured
    //   Given no Claude API key is available (IsConfiguredAsync returns false)
    //   And the measure string is colloquial and does not resolve deterministically
    //   When NormalizeAsync is called
    //   Then the service writes the unresolved token to the review queue
    //   And the result's WasDeferredToQueue flag is true
    //   And Claude.ResolveUnitOfMeasureAsync is never invoked

    [Fact]
    public async Task NormalizeAsync_WithoutClaudeKey_DefersToReviewQueueInsteadOfCallingClaude()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_WithoutClaudeKey_DefersToReviewQueueInsteadOfCallingClaude));
        var claudeMock = CreateStrictClaudeMock(); // Strict: any Claude call would fail the test
        var service = BuildService(dbContext, claudeMock.Object, claudeConfigured: false);

        // Act — colloquial measure with no deterministic match
        var result = await service.NormalizeAsync("a sprinkle", "paprika");

        // Assert — deferred rather than Claude-resolved
        result.WasDeferredToQueue.Should().BeTrue();
        result.WasClaudeResolved.Should().BeFalse();

        var queued = await dbContext.UnresolvedUnitOfMeasureTokens.SingleAsync();
        queued.UnitToken.Should().Be("a sprinkle");

        // Strict mock fails the test if ResolveUnitOfMeasureAsync was ever invoked
    }

    // ── Ingest robustness: long Kaggle measure strings must not overflow columns
    //
    // Scenario: NormalizeOrDeferAsync with an over-length measure truncates before persist
    //   Given a measure string whose parsed unit token is longer than the 100-char column cap
    //   And an ingredient name longer than the 500-char sample-string column cap
    //   When NormalizeOrDeferAsync routes the row to the review queue
    //   Then the persisted UnitToken is truncated to 100 chars
    //   And the persisted SampleMeasureString and SampleIngredientContext are truncated to 500 chars
    //   And no 22001 "value too long" exception is thrown

    [Fact]
    public async Task NormalizeOrDeferAsync_OverLengthTokenAndContext_TruncatesAtColumnCaps()
    {
        // Arrange — a measure with no leading number so the whole string becomes the unit token
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeOrDeferAsync_OverLengthTokenAndContext_TruncatesAtColumnCaps));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        var longMeasure = new string('a', 700);          // over both the 100-char token cap and the 500-char sample cap
        var longIngredient = new string('b', 700);       // over the 500-char sample-ingredient-context cap

        // Act
        var result = await service.NormalizeOrDeferAsync(longMeasure, longIngredient);

        // Assert
        result.WasDeferredToQueue.Should().BeTrue();
        var queued = await dbContext.UnresolvedUnitOfMeasureTokens.SingleAsync();
        queued.UnitToken.Length.Should().Be(100);
        queued.SampleMeasureString.Length.Should().Be(500);
        queued.SampleIngredientContext.Length.Should().Be(500);
    }

    // Scenario: Re-queueing the same long token increments the count instead of inserting a duplicate
    //   Given a long measure has already been queued (truncated to 100 chars)
    //   When NormalizeOrDeferAsync is called again with the same long measure
    //   Then the existing row's Count is incremented
    //   And no second row is inserted

    [Fact]
    public async Task NormalizeOrDeferAsync_RequeueSameOverLengthToken_IncrementsExistingRow()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeOrDeferAsync_RequeueSameOverLengthToken_IncrementsExistingRow));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        var longMeasure = new string('x', 250);

        // Act
        await service.NormalizeOrDeferAsync(longMeasure, "some ingredient");
        await service.NormalizeOrDeferAsync(longMeasure, "another ingredient");

        // Assert — one row, count=2, because truncated lookup key matches
        var queued = await dbContext.UnresolvedUnitOfMeasureTokens.SingleAsync();
        queued.Count.Should().Be(2);
    }
}
