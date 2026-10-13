// Feature: unit of measure Token Parser
//
// Scenario: Integer quantity with unit is split cleanly
//   Given the measure string "2 cups"
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the quantity is 2 and the unit token is "cups"
//
// Scenario: Decimal quantity is parsed
//   Given the measure string "1.5 tbsp"
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the quantity is 1.5
//
// Scenario: Fraction quantity is parsed to its decimal value
//   Given the measure string "1/2 cup"
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the quantity is 0.5
//
// Scenario: Adjacent numeric + unit (no space) is still split
//   Given the measure string "500g"
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the quantity is 500 and the unit token is "g"
//
// Scenario: Measure string with no leading digit returns (0, whole-string)
//   Given the measure string "a knob"
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the quantity is 0 and the unit token is "a knob"
//
// Scenario: Fraction with zero denominator returns 0 quantity without throwing
//   Given the measure string "1/0 cup"
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the quantity is 0 (no DivideByZeroException)
//
// Scenario: Leading and trailing whitespace is trimmed
//   Given the measure string "  2 cups  "
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the unit token has no leading/trailing whitespace
//
// MEP-063: ParseWithIngredientContext
//
// Scenario: Mixed number is summed
//   Given the measure string "1 1/2 cups"
//   When UnitOfMeasureTokenParser.Parse is called
//   Then the quantity is 1.5 and the unit token is "cups"
//
// Scenario: Unrecognized leading word becomes the deferral token (MEP-063 AC1)
//   Given the measure string "2 sprigs thyme" for ingredient "thyme"
//   When ParseWithIngredientContext is called
//   Then DeferralUnitToken is "sprigs" and IsCountWithIngredientNoun is false
//
// Scenario: Leading word that is the ingredient noun is a count (MEP-063 AC2)
//   Given the measure string "4 chicken breasts" for ingredient "chicken"
//   When ParseWithIngredientContext is called
//   Then IsCountWithIngredientNoun is true
//
// Scenario: Plural ingredient nouns match singular ingredient names
//   Given "3 eggs" for "egg" and "2 tomatoes" for "tomato"
//   When ParseWithIngredientContext is called
//   Then IsCountWithIngredientNoun is true for both
//
// Scenario: Size descriptors are skipped before the noun
//   Given "2 large eggs" for "egg"
//   When ParseWithIngredientContext is called
//   Then IsCountWithIngredientNoun is true
//
// Scenario: Abbreviation followed by ingredient yields the abbreviation as a candidate
//   Given "1 c. flour" for "flour"
//   When ParseWithIngredientContext is called
//   Then the candidates contain "c."
//
// Scenario: Two-word unit is offered as the first candidate
//   Given "2 fl oz milk" for "milk"
//   When ParseWithIngredientContext is called
//   Then the first candidate is "fl oz"
//
// Scenario: Leading parenthetical is skipped
//   Given "1 (8 oz.) pkg. cream cheese" for "cream cheese"
//   When ParseWithIngredientContext is called
//   Then DeferralUnitToken is "pkg."
//
// Scenario: Trailing comma is trimmed from the token
//   Given "2 cloves, minced" for "garlic"
//   When ParseWithIngredientContext is called
//   Then DeferralUnitToken is "cloves"
//
// Scenario: Bare quantity is a count with no token
//   Given "5" for "eggs"
//   When ParseWithIngredientContext is called
//   Then IsCountWithIngredientNoun is true, there are no candidates, and the token is empty
//
// Scenario: Missing quantity preserves the whole remainder as the token
//   Given "a smidge" for "cayenne"
//   When ParseWithIngredientContext is called
//   Then the quantity is 0, DeferralUnitToken is "a smidge", and IsCountWithIngredientNoun is false
//
// Scenario: Null ingredient name never yields a count noun
//   Given "4 chicken breasts" with a null ingredient name
//   When ParseWithIngredientContext is called
//   Then IsCountWithIngredientNoun is false

using FluentAssertions;
using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Unit.Common;

public class UnitOfMeasureTokenParserTests
{
    [Fact]
    public void Parse_IntegerWithUnit_SplitsCleanly()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("2 cups");

        quantity.Should().Be(2m);
        token.Should().Be("cups");
    }

    [Fact]
    public void Parse_DecimalQuantity_IsParsed()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("1.5 tbsp");

        quantity.Should().Be(1.5m);
        token.Should().Be("tbsp");
    }

    [Fact]
    public void Parse_FractionQuantity_IsParsedAsDecimal()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("1/2 cup");

        quantity.Should().Be(0.5m);
        token.Should().Be("cup");
    }

    [Fact]
    public void Parse_QuarterFraction_IsParsedAsDecimal()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("1/4 tsp");

        quantity.Should().Be(0.25m);
        token.Should().Be("tsp");
    }

    [Fact]
    public void Parse_AdjacentNumberAndUnit_SplitsCorrectly()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("500g");

        quantity.Should().Be(500m);
        token.Should().Be("g");
    }

    [Fact]
    public void Parse_NoLeadingDigit_ReturnsZeroAndFullString()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("a knob");

        quantity.Should().Be(0m);
        token.Should().Be("a knob");
    }

    [Fact]
    public void Parse_FractionWithZeroDenominator_ReturnsZeroAndDoesNotThrow()
    {
        Action act = () =>
        {
            var (quantity, _) = UnitOfMeasureTokenParser.Parse("1/0 cup");
            quantity.Should().Be(0m);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_MeasureStringWithSurroundingWhitespace_IsTrimmed()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("  2 cups  ");

        quantity.Should().Be(2m);
        token.Should().Be("cups");
    }

    [Fact]
    public void Parse_EmptyString_ReturnsZeroAndEmptyToken()
    {
        var (quantity, token) = UnitOfMeasureTokenParser.Parse(string.Empty);

        quantity.Should().Be(0m);
        token.Should().BeEmpty();
    }

    // ── MEP-063: Parse (mixed numbers) ───────────────────────────────────────

    [Fact]
    public void Parse_MixedNumber_ReturnsSummedQuantityAndUnit()
    {
        // Arrange / Act
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("1 1/2 cups");

        // Assert
        quantity.Should().Be(1.5m);
        token.Should().Be("cups");
    }

    // ── MEP-063: ParseWithIngredientContext ──────────────────────────────────

    [Fact]
    public void ParseWithIngredientContext_AbbreviationWithIngredient_CandidatesContainAbbreviation()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("1 c. flour", "flour");

        // Assert
        result.Quantity.Should().Be(1m);
        result.UnitTokenCandidates.Should().Contain("c.");
    }

    [Fact]
    public void ParseWithIngredientContext_BareQuantity_IsCountWithNoToken()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("5", "eggs");

        // Assert
        result.Quantity.Should().Be(5m);
        result.IsCountWithIngredientNoun.Should().BeTrue();
        result.UnitTokenCandidates.Should().BeEmpty();
        result.DeferralUnitToken.Should().BeEmpty();
    }

    [Fact]
    public void ParseWithIngredientContext_LeadingParenthetical_IsSkipped()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext(
            "1 (8 oz.) pkg. cream cheese", "cream cheese");

        // Assert
        result.Quantity.Should().Be(1m);
        result.DeferralUnitToken.Should().Be("pkg.");
        result.UnitTokenCandidates.Should().Contain("pkg.");
        result.UnitTokenCandidates.Should().NotContain(c => c.Contains('('));
    }

    [Fact]
    public void ParseWithIngredientContext_LeadingWordIsIngredientNoun_IsCountWithIngredientNoun()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("4 chicken breasts", "chicken");

        // Assert
        result.Quantity.Should().Be(4m);
        result.IsCountWithIngredientNoun.Should().BeTrue();
    }

    [Fact]
    public void ParseWithIngredientContext_NoQuantity_PreservesWholeRemainderToken()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("a smidge", "cayenne");

        // Assert
        result.Quantity.Should().Be(0m);
        result.UnitTokenCandidates.Should().Equal("a smidge");
        result.DeferralUnitToken.Should().Be("a smidge");
        result.IsCountWithIngredientNoun.Should().BeFalse();
    }

    [Fact]
    public void ParseWithIngredientContext_NullIngredientName_IsNotCountNoun()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("4 chicken breasts", null);

        // Assert
        result.IsCountWithIngredientNoun.Should().BeFalse();
        result.DeferralUnitToken.Should().Be("chicken");
    }

    [Theory]
    [InlineData("3 eggs", "egg")]
    [InlineData("2 tomatoes", "tomato")]
    public void ParseWithIngredientContext_PluralIngredientNoun_IsCountWithIngredientNoun(
        string measureString,
        string ingredientName)
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, ingredientName);

        // Assert
        result.IsCountWithIngredientNoun.Should().BeTrue();
    }

    [Fact]
    public void ParseWithIngredientContext_SizeDescriptorBeforeNoun_IsCountWithIngredientNoun()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("2 large eggs", "egg");

        // Assert
        result.Quantity.Should().Be(2m);
        result.IsCountWithIngredientNoun.Should().BeTrue();
    }

    [Fact]
    public void ParseWithIngredientContext_TrailingComma_IsTrimmedFromToken()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("2 cloves, minced", "garlic");

        // Assert
        result.DeferralUnitToken.Should().Be("cloves");
        result.UnitTokenCandidates.Should().Contain("cloves");
    }

    [Fact]
    public void ParseWithIngredientContext_TwoWordUnit_TwoWordCandidateFirst()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("2 fl oz milk", "milk");

        // Assert
        result.UnitTokenCandidates.First().Should().Be("fl oz");
        result.UnitTokenCandidates.Should().Contain("fl");
    }

    [Fact]
    public void ParseWithIngredientContext_UnrecognizedLeadingWord_SetsDeferralTokenToLeadingWord()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("2 sprigs thyme", "thyme");

        // Assert
        result.Quantity.Should().Be(2m);
        result.DeferralUnitToken.Should().Be("sprigs");
        result.IsCountWithIngredientNoun.Should().BeFalse();
    }

    // ── MEP-067: ranges, junk, to-taste, size descriptors, butter sticks ─────
    //
    // Scenario: Hyphenated mixed number is summed
    //   Given "1-1/2 cups flour"
    //   When Parse is called
    //   Then the quantity is 1.5 and the unit token is "cups flour"
    //
    // Scenario: Range keeps the lower bound
    //   Given "2-3 tbsp", "2 - 3 tsp", "1 to 1 1/4 c." and "1/2-1 tsp"
    //   When Parse is called
    //   Then the lower bound is the quantity and no fragment such as "-3" remains
    //
    // Scenario: Junk leading word is skipped
    //   Given "2 or more bananas" and "1 - pound"
    //   When ParseWithIngredientContext is called
    //   Then junk is never a candidate or deferral token
    //
    // Scenario: Junk-only and to-taste lines are flagged
    //   Given "None", "-", "None." and "salt and pepper (to taste)"
    //   When ParseWithIngredientContext is called
    //   Then IsJunkLine or IsToTaste is true and no candidates are produced
    //
    // Scenario: A line with a quantity is not to-taste
    //   Given "1 tsp salt"
    //   When ParseWithIngredientContext is called
    //   Then IsToTaste is false
    //
    // Scenario: Size descriptors are skipped before a unit word
    //   Given "2 large cloves garlic" and "1 large can tomatoes"
    //   When ParseWithIngredientContext is called
    //   Then the candidates start at the unit word and StartsWithSizeDescriptor is true
    //
    // Scenario: Butter sticks carry a defined weight
    //   Given "2 sticks butter" and margarine, unsalted butter, or a null ingredient name
    //   When ParseWithIngredientContext is called
    //   Then IngredientSpecificGramsPerUnit is 113; peanut butter and cinnamon stick get none

    [Fact]
    public void Parse_HyphenatedMixedNumber_SumsWholeAndFraction()
    {
        // Arrange / Act
        var (quantity, token) = UnitOfMeasureTokenParser.Parse("1-1/2 cups flour");

        // Assert
        quantity.Should().Be(1.5m);
        token.Should().Be("cups flour");
    }

    [Theory]
    [InlineData("2-3 tbsp", 2, "tbsp")]
    [InlineData("2 - 3 tsp", 2, "tsp")]
    [InlineData("1 to 1 1/4 c.", 1, "c.")]
    [InlineData("1/2-1 tsp", 0.5, "tsp")]
    [InlineData("1 or 2 eggs", 1, "eggs")]
    [InlineData("2–3 cups", 2, "cups")]
    public void Parse_Range_KeepsLowerBoundAndDropsUpperBound(string measureString, double expectedQuantity, string expectedToken)
    {
        // Arrange / Act
        var (quantity, token) = UnitOfMeasureTokenParser.Parse(measureString);

        // Assert
        quantity.Should().Be((decimal)expectedQuantity);
        token.Should().Be(expectedToken);
    }

    [Theory]
    [InlineData("2 or more bananas", "bananas")]
    [InlineData("1 - pound", "pound")]
    public void ParseWithIngredientContext_JunkLeadingWord_IsSkipped(string measureString, string expectedToken)
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, null);

        // Assert
        result.DeferralUnitToken.Should().Be(expectedToken);
        result.UnitTokenCandidates.Should().Contain(expectedToken);
        result.UnitTokenCandidates.Should().NotContain(c => c.StartsWith("or") || c.StartsWith("-") || c.StartsWith("more"));
    }

    [Fact]
    public void ParseWithIngredientContext_OnlyJunkAfterQuantity_IsCountWithIngredientNoun()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("2 or more", "banana");

        // Assert
        result.IsCountWithIngredientNoun.Should().BeTrue();
        result.UnitTokenCandidates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("None")]
    [InlineData("-")]
    [InlineData("None.")]
    [InlineData("to")]
    public void ParseWithIngredientContext_JunkOnlyLine_IsJunkWithNoCandidates(string measureString)
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, "salt");

        // Assert
        result.IsJunkLine.Should().BeTrue();
        result.IsToTaste.Should().BeFalse();
        result.UnitTokenCandidates.Should().BeEmpty();
        result.DeferralUnitToken.Should().BeEmpty();
    }

    [Theory]
    [InlineData("salt")]
    [InlineData("pepper")]
    [InlineData("salt and pepper")]
    [InlineData("salt to taste")]
    [InlineData("salt and pepper (to taste)")]
    [InlineData("Garlic powder to taste")]
    public void ParseWithIngredientContext_ToTasteLine_IsToTasteWithNoCandidates(string measureString)
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, "salt");

        // Assert
        result.IsToTaste.Should().BeTrue();
        result.IsJunkLine.Should().BeFalse();
        result.UnitTokenCandidates.Should().BeEmpty();
        result.DeferralUnitToken.Should().BeEmpty();
    }

    [Fact]
    public void ParseWithIngredientContext_QuantityWithSalt_IsNotToTaste()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("1 tsp salt", "salt");

        // Assert
        result.IsToTaste.Should().BeFalse();
        result.UnitTokenCandidates.Should().Contain("tsp");
    }

    [Fact]
    public void ParseWithIngredientContext_SizeDescriptorBeforeUnitWord_SkipsDescriptor()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("2 large cloves garlic", "garlic");

        // Assert
        result.UnitTokenCandidates.Should().Equal("cloves garlic", "cloves");
        result.DeferralUnitToken.Should().Be("cloves");
        result.StartsWithSizeDescriptor.Should().BeTrue();
    }

    [Fact]
    public void ParseWithIngredientContext_SizeDescriptorBeforeContainerWord_YieldsContainerWordFirst()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("1 large can tomatoes", "tomatoes");

        // Assert
        result.DeferralUnitToken.Should().Be("can");
        result.UnitTokenCandidates.Should().Contain("can");
        result.UnitTokenCandidates.Should().NotContain("large");
    }

    [Fact]
    public void ParseWithIngredientContext_SizeDescriptorBeforeNonUnit_IsCountFallbackEligible()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("1 large yellow onion", "onion");

        // Assert
        result.StartsWithSizeDescriptor.Should().BeTrue();
        result.IsCountFallbackEligible.Should().BeTrue();
    }

    [Fact]
    public void ParseWithIngredientContext_NoSizeDescriptorAndNoNoun_IsNotCountFallbackEligible()
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext("2 sprigs thyme", "thyme");

        // Assert
        result.IsCountFallbackEligible.Should().BeFalse();
    }

    [Theory]
    [InlineData("2 sticks butter", "butter")]
    [InlineData("1 stick margarine", "margarine")]
    [InlineData("1 stick unsalted butter", "unsalted butter")]
    [InlineData("1 stick. butter", "Butter")]
    [InlineData("2 sticks butter", null)]
    public void ParseWithIngredientContext_ButterOrMargarineSticks_SetsGramsPerStick(string measureString, string? ingredientName)
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, ingredientName);

        // Assert
        result.IngredientSpecificGramsPerUnit.Should().Be(113m);
    }

    [Theory]
    [InlineData("1 stick peanut butter", "peanut butter")]
    [InlineData("1 stick cinnamon", "cinnamon")]
    [InlineData("2 sticks celery", "celery")]
    [InlineData("1 stick almond butter", null)]
    public void ParseWithIngredientContext_NonButterSticks_DoNotSetGramsPerStick(string measureString, string? ingredientName)
    {
        // Arrange / Act
        var result = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, ingredientName);

        // Assert
        result.IngredientSpecificGramsPerUnit.Should().BeNull();
    }
}
