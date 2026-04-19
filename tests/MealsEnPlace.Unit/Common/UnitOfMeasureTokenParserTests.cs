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
}
