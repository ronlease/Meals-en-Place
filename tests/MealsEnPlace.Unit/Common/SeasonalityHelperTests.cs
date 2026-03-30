// Feature: Seasonality — IsInSeason Helper
//
// Scenario: Month falls within a simple forward window
//   Given a window from June to August
//   When the current month is July
//   Then IsInSeason returns true
//
// Scenario: Month is the first day of a forward window (inclusive lower bound)
//   Given a window from June to August
//   When the current month is June
//   Then IsInSeason returns true
//
// Scenario: Month is the last day of a forward window (inclusive upper bound)
//   Given a window from June to August
//   When the current month is August
//   Then IsInSeason returns true
//
// Scenario: Month falls outside a simple forward window
//   Given a window from June to August
//   When the current month is October
//   Then IsInSeason returns false
//
// Scenario: Month falls in a wrap-around window — in season (winter side)
//   Given a window from November to February (wraps past December)
//   When the current month is January
//   Then IsInSeason returns true
//
// Scenario: Month falls in a wrap-around window — at start boundary
//   Given a window from November to February
//   When the current month is November
//   Then IsInSeason returns true
//
// Scenario: Month falls in a wrap-around window — at end boundary
//   Given a window from November to February
//   When the current month is February
//   Then IsInSeason returns true
//
// Scenario: Month falls in a wrap-around window — spans December
//   Given a window from November to February
//   When the current month is December
//   Then IsInSeason returns true
//
// Scenario: Month falls outside a wrap-around window
//   Given a window from November to February
//   When the current month is July
//   Then IsInSeason returns false
//
// Scenario: Month falls just before start of wrap-around window
//   Given a window from November to February
//   When the current month is October
//   Then IsInSeason returns false
//
// Scenario: Month falls just after end of wrap-around window
//   Given a window from November to February
//   When the current month is March
//   Then IsInSeason returns false
//
// Scenario: Single-month window — month matches
//   Given a window from July to July
//   When the current month is July
//   Then IsInSeason returns true
//
// Scenario: Single-month window — month does not match
//   Given a window from July to July
//   When the current month is August
//   Then IsInSeason returns false
//
// Scenario: Year-round window (January to December) — always in season
//   Given a window from January to December
//   When the current month is any month
//   Then IsInSeason returns true
//
// Scenario: Dual-window produce — month inside first window
//   Given produce with two windows: March–May and September–November
//   When the current month is April
//   Then the first window returns true
//
// Scenario: Dual-window produce — month inside second window
//   Given produce with two windows: March–May and September–November
//   When the current month is October
//   Then the second window returns true
//
// Scenario: Dual-window produce — month outside both windows
//   Given produce with two windows: March–May and September–November
//   When the current month is January
//   Then both windows return false

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Unit.Common;

public class SeasonalityHelperTests
{
    // ── Forward (non-wrapping) windows ────────────────────────────────────────

    [Fact]
    public void IsInSeason_MonthInsideForwardWindow_ReturnsTrue()
    {
        // Arrange
        const Month current = Month.July;
        const Month start = Month.June;
        const Month end = Month.August;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthAtLowerBoundOfForwardWindow_ReturnsTrue()
    {
        // Arrange — inclusive lower bound
        const Month current = Month.June;
        const Month start = Month.June;
        const Month end = Month.August;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthAtUpperBoundOfForwardWindow_ReturnsTrue()
    {
        // Arrange — inclusive upper bound
        const Month current = Month.August;
        const Month start = Month.June;
        const Month end = Month.August;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthOutsideForwardWindow_ReturnsFalse()
    {
        // Arrange
        const Month current = Month.October;
        const Month start = Month.June;
        const Month end = Month.August;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInSeason_MonthJustBeforeForwardWindowStart_ReturnsFalse()
    {
        // Arrange
        const Month current = Month.May;
        const Month start = Month.June;
        const Month end = Month.August;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInSeason_MonthJustAfterForwardWindowEnd_ReturnsFalse()
    {
        // Arrange
        const Month current = Month.September;
        const Month start = Month.June;
        const Month end = Month.August;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    // ── Wrap-around windows (start > end, e.g., November–February) ────────────

    [Fact]
    public void IsInSeason_MonthAtStartOfWrapAroundWindow_ReturnsTrue()
    {
        // Arrange
        const Month current = Month.November;
        const Month start = Month.November;
        const Month end = Month.February;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthAtEndOfWrapAroundWindow_ReturnsTrue()
    {
        // Arrange
        const Month current = Month.February;
        const Month start = Month.November;
        const Month end = Month.February;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthInsideWrapAroundWindowDecember_ReturnsTrue()
    {
        // Arrange — December is in the wrapped portion (after November, wraps past December)
        const Month current = Month.December;
        const Month start = Month.November;
        const Month end = Month.February;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthInsideWrapAroundWindowJanuary_ReturnsTrue()
    {
        // Arrange — January is in the early portion after the year wrap
        const Month current = Month.January;
        const Month start = Month.November;
        const Month end = Month.February;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthJustBeforeStartOfWrapAroundWindow_ReturnsFalse()
    {
        // Arrange
        const Month current = Month.October;
        const Month start = Month.November;
        const Month end = Month.February;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInSeason_MonthJustAfterEndOfWrapAroundWindow_ReturnsFalse()
    {
        // Arrange
        const Month current = Month.March;
        const Month start = Month.November;
        const Month end = Month.February;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInSeason_MonthClearlyOutsideWrapAroundWindow_ReturnsFalse()
    {
        // Arrange — July is well outside November–February
        const Month current = Month.July;
        const Month start = Month.November;
        const Month end = Month.February;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    // ── Single-month windows ──────────────────────────────────────────────────

    [Fact]
    public void IsInSeason_SingleMonthWindowAndMonthMatches_ReturnsTrue()
    {
        // Arrange
        const Month current = Month.July;
        const Month start = Month.July;
        const Month end = Month.July;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_SingleMonthWindowAndMonthDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        const Month current = Month.August;
        const Month start = Month.July;
        const Month end = Month.July;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    // ── Year-round window ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(Month.January)]
    [InlineData(Month.April)]
    [InlineData(Month.July)]
    [InlineData(Month.October)]
    [InlineData(Month.December)]
    public void IsInSeason_YearRoundWindow_AlwaysReturnsTrue(Month current)
    {
        // Arrange — January to December covers all months
        const Month start = Month.January;
        const Month end = Month.December;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeTrue();
    }

    // ── Dual-window produce (e.g., Kale/Broccoli: spring and autumn windows) ──

    [Fact]
    public void IsInSeason_MonthInsideFirstWindowOfDualWindowProduce_ReturnsTrue()
    {
        // Arrange — first window: March–May
        const Month current = Month.April;
        const Month start1 = Month.March;
        const Month end1 = Month.May;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start1, end1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthInsideSecondWindowOfDualWindowProduce_ReturnsTrue()
    {
        // Arrange — second window: September–November
        const Month current = Month.October;
        const Month start2 = Month.September;
        const Month end2 = Month.November;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start2, end2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInSeason_MonthOutsideBothWindowsOfDualWindowProduce_ReturnsFalseForBothChecks()
    {
        // Arrange — January falls outside March–May and September–November
        const Month current = Month.January;
        const Month start1 = Month.March;
        const Month end1 = Month.May;
        const Month start2 = Month.September;
        const Month end2 = Month.November;

        // Act
        var result1 = SeasonalityHelper.IsInSeason(current, start1, end1);
        var result2 = SeasonalityHelper.IsInSeason(current, start2, end2);

        // Assert — in-season only when either window matches; both must be false here
        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    // ── Boundary: month immediately adjacent to a window edge ─────────────────

    [Fact]
    public void IsInSeason_MonthOneBeforeLowerBound_ReturnsFalse()
    {
        // Arrange — window April–September; March is one month before the start
        const Month current = Month.March;
        const Month start = Month.April;
        const Month end = Month.September;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInSeason_MonthOneAfterUpperBound_ReturnsFalse()
    {
        // Arrange — window April–September; October is one month after the end
        const Month current = Month.October;
        const Month start = Month.April;
        const Month end = Month.September;

        // Act
        var result = SeasonalityHelper.IsInSeason(current, start, end);

        // Assert
        result.Should().BeFalse();
    }
}
