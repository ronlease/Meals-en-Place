// Feature: UOM Normalization — Normalization Service
//
// Scenario: Standard unit is resolved deterministically and Claude is not invoked
//   Given the measure string "8 oz"
//   And "oz" exists in the UOM table
//   When NormalizeAsync is called
//   Then the result Quantity is 8
//   And UomAbbreviation is "oz"
//   And WasClaudeResolved is false
//   And Confidence is High
//   And Claude is never called
//
// Scenario: Standard volume unit is resolved deterministically
//   Given the measure string "2 cups"
//   And "cup" exists in the UOM table
//   When NormalizeAsync is called
//   Then the result Quantity is 2
//   And UomAbbreviation is "cup"
//   And WasClaudeResolved is false
//   And Confidence is High
//   And Claude is never called
//
// Scenario: Fraction measure string is parsed and resolved deterministically
//   Given the measure string "1/2 cup"
//   And "cup" exists in the UOM table
//   When NormalizeAsync is called
//   Then the result Quantity is 0.5
//   And UomAbbreviation is "cup"
//   And WasClaudeResolved is false
//   And Confidence is High
//   And Claude is never called
//
// Scenario: Quarter fraction is parsed correctly
//   Given the measure string "1/4 tsp"
//   And "tsp" exists in the UOM table
//   When NormalizeAsync is called
//   Then the result Quantity is 0.25
//   And WasClaudeResolved is false
//
// Scenario: Colloquial unit falls back to Claude
//   Given the measure string "a knob" for ingredient "butter"
//   And "knob" does not exist in the UOM table
//   When NormalizeAsync is called
//   Then Claude is invoked via ResolveUomAsync
//   And WasClaudeResolved is true
//   And the result reflects the Claude-returned quantity and unit
//
// Scenario: WasClaudeResolved is true on any Claude-backed result
//   Given a measure string whose unit token is not in the UOM table
//   When NormalizeAsync is called
//   Then WasClaudeResolved is true regardless of Confidence
//
// Scenario: Claude returns a known UOM abbreviation — UomId is populated
//   Given Claude resolves a colloquial string to "g"
//   And "g" exists in the UOM table
//   When NormalizeAsync is called
//   Then UomId equals the seeded Gram ID
//   And UomAbbreviation is "g"
//
// Scenario: Claude returns an unknown abbreviation — UomId is Guid.Empty
//   Given Claude resolves a colloquial string to an unmapped abbreviation "splash"
//   And "splash" does not exist in the UOM table
//   When NormalizeAsync is called
//   Then UomId is Guid.Empty
//   And UomAbbreviation is "splash"
//
// Scenario: Unknown unit with no Claude match returns Low confidence
//   Given a measure string with an unmapped unit token
//   And Claude returns Confidence.Low with an empty ResolvedUom
//   When NormalizeAsync is called
//   Then Confidence is Low
//   And WasClaudeResolved is true
//   And UomId is Guid.Empty
//
// Scenario: Unit token match is case-insensitive
//   Given the measure string "500G" (uppercase G)
//   And "g" exists in the UOM table with lowercase abbreviation
//   When NormalizeAsync is called
//   Then the unit is resolved deterministically
//   And WasClaudeResolved is false
//
// Scenario: WasClaudeResolved is false for all deterministic resolutions
//   Given the measure string "1 tbsp"
//   And "tbsp" exists in the UOM table
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
//   Then ResolveUomAsync is called exactly once on the mock

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MealsEnPlace.Unit.Common;

public class UomNormalizationServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MealsEnPlaceDbContext CreateSeededDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var dbContext = new MealsEnPlaceDbContext(options);

        // Seed a representative subset of UOM rows sufficient for normalization tests.
        // Base units first (no foreign-key dependency).
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
                Abbreviation = "oz",
                BaseUomId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 28.350m,
                Id = UnitOfMeasureConfiguration.OzId,
                Name = "Ounce",
                UomType = UomType.Weight
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
            new UnitOfMeasure
            {
                Abbreviation = "lb",
                BaseUomId = UnitOfMeasureConfiguration.GramId,
                ConversionFactor = 453.592m,
                Id = UnitOfMeasureConfiguration.LbId,
                Name = "Pound",
                UomType = UomType.Weight
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

    private static Mock<IClaudeService> CreateStrictClaudeMock() =>
        new(MockBehavior.Strict);

    private static UomNormalizationService BuildService(
        MealsEnPlaceDbContext dbContext,
        IClaudeService claudeService) =>
        new(claudeService, dbContext);

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
        result.UomAbbreviation.Should().Be("oz");
        result.UomId.Should().Be(UnitOfMeasureConfiguration.OzId);
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
        result.UomAbbreviation.Should().Be("cup");
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
        result.UomAbbreviation.Should().Be("cup");
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
        result.UomAbbreviation.Should().Be("g");
    }

    // ── Colloquial unit — Claude fallback path ────────────────────────────────

    [Fact]
    public async Task NormalizeAsync_ColloquialKnobOfButter_InvokesClaudeAndReturnsClaudeResult()
    {
        // Arrange
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ColloquialKnobOfButter_InvokesClaudeAndReturnsClaudeResult));
        var claudeMock = new Mock<IClaudeService>(MockBehavior.Strict);
        claudeMock
            .Setup(c => c.ResolveUomAsync("a knob", "butter"))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.High,
                Notes = "Assumed standard knob size; user may override.",
                ResolvedQuantity = 15m,
                ResolvedUom = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a knob", "butter");

        // Assert
        result.WasClaudeResolved.Should().BeTrue();
        result.Quantity.Should().Be(15m);
        result.UomAbbreviation.Should().Be("g");
        result.UomId.Should().Be(UnitOfMeasureConfiguration.GramId);
        result.Confidence.Should().Be(ClaudeConfidence.High);
        claudeMock.Verify(c => c.ResolveUomAsync("a knob", "butter"), Times.Once);
    }

    [Fact]
    public async Task NormalizeAsync_ColloquialUnit_WasClaudeResolvedIsTrue()
    {
        // Arrange — any unmapped unit token triggers the Claude path
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ColloquialUnit_WasClaudeResolvedIsTrue));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.Medium,
                Notes = string.Empty,
                ResolvedQuantity = 10m,
                ResolvedUom = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a pinch", "cayenne");

        // Assert
        result.WasClaudeResolved.Should().BeTrue();
    }

    [Fact]
    public async Task NormalizeAsync_ClaudeResolvesToKnownAbbreviation_UomIdIsPopulated()
    {
        // Arrange — Claude returns "g", which is seeded in the database.
        // Use a no-quantity measure string so the MEP-026 count-with-ingredient-noun
        // fallback does not short-circuit this Claude path.
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ClaudeResolvesToKnownAbbreviation_UomIdIsPopulated));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.High,
                Notes = string.Empty,
                ResolvedQuantity = 15m,
                ResolvedUom = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a head", "garlic");

        // Assert
        result.UomId.Should().Be(UnitOfMeasureConfiguration.GramId);
        result.UomAbbreviation.Should().Be("g");
        result.WasClaudeResolved.Should().BeTrue();
    }

    [Fact]
    public async Task NormalizeAsync_ClaudeResolvesToUnknownAbbreviation_UomIdIsEmpty()
    {
        // Arrange — Claude returns "splash", which is not in the database
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_ClaudeResolvesToUnknownAbbreviation_UomIdIsEmpty));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.Low,
                Notes = "Could not map to a canonical unit.",
                ResolvedQuantity = 0m,
                ResolvedUom = "splash"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a splash", "vinegar");

        // Assert
        result.UomId.Should().Be(Guid.Empty);
        result.UomAbbreviation.Should().Be("splash");
        result.WasClaudeResolved.Should().BeTrue();
    }

    // ── Low confidence — unknown unit with no Claude match ────────────────────

    [Fact]
    public async Task NormalizeAsync_UnknownUnitWithLowConfidenceClaudeResult_ReturnsLowConfidence()
    {
        // Arrange — Claude cannot resolve; returns Low confidence and empty ResolvedUom.
        // Use a no-quantity measure string so the MEP-026 count-with-ingredient-noun
        // fallback does not short-circuit this Claude path.
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_UnknownUnitWithLowConfidenceClaudeResult_ReturnsLowConfidence));
        var claudeMock = new Mock<IClaudeService>();
        claudeMock
            .Setup(c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.Low,
                Notes = "Claude integration not yet configured. Please declare the quantity and unit manually.",
                ResolvedQuantity = 0m,
                ResolvedUom = string.Empty
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("a schmear", "cream cheese");

        // Assert
        result.Confidence.Should().Be(ClaudeConfidence.Low);
        result.WasClaudeResolved.Should().BeTrue();
        result.UomId.Should().Be(Guid.Empty);
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
            .Setup(c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.High,
                Notes = expectedNotes,
                ResolvedQuantity = 15m,
                ResolvedUom = "g"
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
            .Setup(c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.Medium,
                Notes = string.Empty,
                ResolvedQuantity = 5m,
                ResolvedUom = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        await service.NormalizeAsync("a sprinkle", "paprika");

        // Assert — exactly one Claude call, not zero and not more than one
        claudeMock.Verify(
            c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    // ── MEP-026: Alias-table lookup ───────────────────────────────────────────
    //
    // Scenario: Dotted-abbreviation alias resolves deterministically
    //   Given the measure string "1 c. flour"
    //   And an alias row "c." maps to the Cup UnitOfMeasure
    //   When NormalizeAsync is called
    //   Then the result resolves to the Cup UOM with quantity 1
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
        result.UomId.Should().Be(UnitOfMeasureConfiguration.CupId);
        result.UomAbbreviation.Should().Be("cup");
        result.WasClaudeResolved.Should().BeFalse();
        result.Confidence.Should().Be(ClaudeConfidence.High);
    }

    // Scenario: Plural-form alias resolves deterministically
    //   Given the measure string "2 lbs chicken"
    //   And an alias row "lbs" maps to the Pound UnitOfMeasure
    //   When NormalizeAsync is called
    //   Then the result resolves to the Pound UOM with quantity 2
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
        result.UomId.Should().Be(UnitOfMeasureConfiguration.LbId);
        result.UomAbbreviation.Should().Be("lb");
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
        result.UomId.Should().Be(UnitOfMeasureConfiguration.TbspId);
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
        result.UomId.Should().Be(UnitOfMeasureConfiguration.TbspId);
        result.WasClaudeResolved.Should().BeFalse();
    }

    // ── MEP-026: Count-with-ingredient-noun fallback ──────────────────────────
    //
    // Scenario: Count-with-ingredient-noun defaults to "each"
    //   Given the measure string "4 chicken breasts"
    //   And the quantity parses as 4 but "chicken breasts" is not a UOM or alias
    //   When NormalizeAsync is called
    //   Then the result resolves to the Each UOM with quantity 4
    //   And WasClaudeResolved is false
    //   And Claude is never called

    [Fact]
    public async Task NormalizeAsync_CountWithIngredientNoun_DefaultsToEachWithoutClaude()
    {
        // Arrange — "chicken breasts" is not a UOM, name, or alias
        await using var dbContext = CreateSeededDbContext(nameof(NormalizeAsync_CountWithIngredientNoun_DefaultsToEachWithoutClaude));
        var claudeMock = CreateStrictClaudeMock();
        var service = BuildService(dbContext, claudeMock.Object);

        // Act
        var result = await service.NormalizeAsync("4 chicken breasts", "chicken");

        // Assert
        result.Quantity.Should().Be(4m);
        result.UomId.Should().Be(UnitOfMeasureConfiguration.EachId);
        result.UomAbbreviation.Should().Be("ea");
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
            .Setup(c => c.ResolveUomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UomResolutionResult
            {
                Confidence = ClaudeConfidence.Medium,
                Notes = "Assumed pinch size",
                ResolvedQuantity = 1m,
                ResolvedUom = "g"
            });

        var service = BuildService(dbContext, claudeMock.Object);

        // Act — no leading digit, so quantity is 0 after parsing
        var result = await service.NormalizeAsync("a pinch", "salt");

        // Assert — Claude path, not the count-fallback
        result.WasClaudeResolved.Should().BeTrue();
        claudeMock.Verify(c => c.ResolveUomAsync("a pinch", "salt"), Times.Once);
    }
}
