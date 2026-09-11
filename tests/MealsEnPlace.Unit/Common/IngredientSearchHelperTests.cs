// Feature: IngredientSearchHelper — EscapeILikeWildcards
//
// Scenario: Backslash is escaped to double-backslash
//   Given a search term containing a backslash character
//   When EscapeILikeWildcards is called
//   Then the backslash is doubled (\ → \\)
//
// Scenario: Percent sign is escaped to backslash-percent
//   Given a search term containing a percent character
//   When EscapeILikeWildcards is called
//   Then the percent is preceded by a backslash (% → \%)
//   And the character no longer acts as an ILike wildcard
//
// Scenario: Underscore is escaped to backslash-underscore
//   Given a search term containing an underscore character
//   When EscapeILikeWildcards is called
//   Then the underscore is preceded by a backslash (_ → \_)
//   And the character no longer acts as an ILike single-character wildcard
//
// Scenario: Term containing backslash, percent, and underscore are all escaped
//   Given a search term containing all three ILike metacharacters
//   When EscapeILikeWildcards is called
//   Then all three are escaped in a single pass and in the correct order
//
// Note: The Npgsql (PostgreSQL) branch of ApplySearch — the code path that calls
// EscapeILikeWildcards and uses EF.Functions.ILike — is unreachable when running
// against the EF Core in-memory provider used in unit tests.  This is expected:
// EF.Functions.ILike throws NotSupportedException on any non-Npgsql provider.
// The in-memory fallback (ToLower().Contains / StartsWith) is exercised by the
// controller tests in ReferenceDataControllerTests.cs.  EscapeILikeWildcards is
// extracted as a public-surface (internal) static method specifically so it can be
// tested directly here, independently of the Npgsql provider requirement.
// The Npgsql branch is excluded from the 90% coverage gate implicitly because the
// gate measures line-level coverage and those lines are never reached; the
// ExcludeByAttribute coverlet setting does not suppress them, but the EscapeILikeWildcards
// tests provide direct confidence in the escaping logic that the production branch relies on.

using FluentAssertions;
using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Unit.Common;

public class IngredientSearchHelperTests
{
    // ── EscapeILikeWildcards — backslash ──────────────────────────────────────

    [Fact]
    public void EscapeILikeWildcards_Backslash_EscapedToDoubleBackslash()
    {
        // Arrange
        var term = @"bread\butter";

        // Act
        var escaped = IngredientSearchHelper.EscapeILikeWildcards(term);

        // Assert
        escaped.Should().Be(@"bread\\butter");
    }

    // ── EscapeILikeWildcards — percent ────────────────────────────────────────

    [Fact]
    public void EscapeILikeWildcards_Percent_EscapedToBackslashPercent()
    {
        // Arrange
        var term = "100% Whole Wheat";

        // Act
        var escaped = IngredientSearchHelper.EscapeILikeWildcards(term);

        // Assert
        escaped.Should().Be(@"100\% Whole Wheat");
    }

    // ── EscapeILikeWildcards — underscore ─────────────────────────────────────

    [Fact]
    public void EscapeILikeWildcards_Underscore_EscapedToBackslashUnderscore()
    {
        // Arrange
        var term = "Secret_Sauce";

        // Act
        var escaped = IngredientSearchHelper.EscapeILikeWildcards(term);

        // Assert
        escaped.Should().Be(@"Secret\_Sauce");
    }

    // ── EscapeILikeWildcards — all three metacharacters ──────────────────────

    [Fact]
    public void EscapeILikeWildcards_AllThreeMetacharacters_AllEscapedCorrectly()
    {
        // Arrange — a term that contains \, %, and _ in one string
        // The backslash must be escaped first so that the subsequently-added
        // escape backslashes are not re-escaped in subsequent passes.
        var term = @"50%_fat\blend";

        // Act
        var escaped = IngredientSearchHelper.EscapeILikeWildcards(term);

        // Assert
        // Expected: \ → \\, % → \%, _ → \_  (applied in that order)
        escaped.Should().Be(@"50\%\_fat\\blend");
    }

    // ── EscapeILikeWildcards — plain string is unchanged ──────────────────────

    [Fact]
    public void EscapeILikeWildcards_NoMetacharacters_ReturnedUnchanged()
    {
        // Arrange
        var term = "Chicken Breast";

        // Act
        var escaped = IngredientSearchHelper.EscapeILikeWildcards(term);

        // Assert
        escaped.Should().Be("Chicken Breast");
    }
}
