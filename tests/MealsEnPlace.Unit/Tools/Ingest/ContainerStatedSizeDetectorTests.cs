// Feature: Container stated-size diagnostic (MEP-067)
//
// Scenario: A size next to the container word counts as stated
//   Given a raw ingredient line naming a container
//   When the stated-size detector runs
//   Then it reports the size, normalized quantity and unit
//
// Scenario: A number and unit elsewhere in the line does not count
//   Given a container line whose only measurement is not adjacent to the container word
//   When the stated-size detector runs
//   Then no size is reported

using FluentAssertions;
using MealsEnPlace.Tools.Ingest;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class ContainerStatedSizeDetectorTests
{
    [Theory]
    [InlineData("1 (14.5 oz) can diced tomatoes", 14.5, "oz")]
    [InlineData("1 (8 oz.) pkg. cream cheese", 8, "oz")]
    [InlineData("2 (10 ounce) packages frozen spinach", 10, "oz")]
    [InlineData("1 14 oz. can tomatoes", 14, "oz")]
    [InlineData("1 8-oz. pkg cream cheese", 8, "oz")]
    [InlineData("1 can (15 oz.) beans", 15, "oz")]
    [InlineData("1 (16-ounce) jar salsa", 16, "oz")]
    [InlineData("1 (14 1/2 ounce) can tomatoes", 14.5, "oz")]
    [InlineData("1 (12 fluid ounce) can cola", 12, "fl oz")]
    [InlineData("1 (1 pound) bag carrots", 1, "lb")]
    [InlineData("1 (400 g) can chickpeas", 400, "g")]
    [InlineData("1 (1.5 liter) bottle wine", 1.5, "l")]
    [InlineData("1 package (10 ounces) frozen peas", 10, "oz")]
    [InlineData("2 (14 1/2 oz. each) cans chicken broth", 14.5, "oz")]
    public void Detect_SizeAdjacentToContainerWord_ReportsStatedSize(string line, double quantity, string unit)
    {
        // Given a container line with an adjacent size, When detected
        var result = ContainerStatedSizeDetector.Detect(line);

        // Then the size is stated and parsed
        result.HasStatedSize.Should().BeTrue();
        result.Quantity.Should().Be((decimal)quantity);
        result.UnitAbbreviation.Should().Be(unit);
        result.IsRange.Should().BeFalse();
    }

    [Theory]
    [InlineData("1 (10 to 12 oz) package frozen broccoli")]
    [InlineData("1 (10-12 ounce) can mushrooms")]
    public void Detect_RangeSize_CountsAsStatedAndReportsLowerBound(string line)
    {
        // Given a ranged size, When detected
        var result = ContainerStatedSizeDetector.Detect(line);

        // Then it is stated, flagged as a range, with the lower bound
        result.HasStatedSize.Should().BeTrue();
        result.IsRange.Should().BeTrue();
        result.Quantity.Should().Be(10m);
        result.UnitAbbreviation.Should().Be("oz");
    }

    [Theory]
    [InlineData("1 can tomatoes, drained, cut into 1 inch pieces")]
    [InlineData("1 can tomatoes, 2 cups chopped")]
    [InlineData("1 can chopped tomatoes")]
    [InlineData("2 cups cooked rice (from 1 can)")]
    [InlineData("1 package yeast")]
    [InlineData("1 pecan 14 oz")]
    [InlineData("")]
    [InlineData(null)]
    public void Detect_NoAdjacentSize_ReportsNotStated(string? line)
    {
        // Given a line without a size next to the container word, When detected
        var result = ContainerStatedSizeDetector.Detect(line);

        // Then no size is reported
        result.HasStatedSize.Should().BeFalse();
        result.Quantity.Should().BeNull();
        result.UnitAbbreviation.Should().BeNull();
    }
}
