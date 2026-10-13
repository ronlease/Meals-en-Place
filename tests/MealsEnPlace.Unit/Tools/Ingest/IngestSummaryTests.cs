// Feature: IngestSummary formatter — RecipeReferenceCount backfill line
//
// Scenario: Format output includes "yes" for the backfill line when run in LIVE mode
//   Given an IngestSummary with RecipeReferenceCountBackfilled = true
//   When Format is called
//   Then the output contains "yes" on the "Ref count backfilled" line
//
// Scenario: Format output shows "skipped (dry run)" for the backfill line in dry-run mode
//   Given an IngestSummary with RecipeReferenceCountBackfilled = false
//   When Format is called with DryRun = true
//   Then the output contains "skipped (dry run)" on the "Ref count backfilled" line
//
// Note: The actual SQL UPDATE that recomputes RecipeReferenceCount lives in Program.cs
// (top-level statements) and is not unit-testable.  The tests here verify the
// IngestSummary flag and its Format output — the observable contract that callers use
// to confirm whether the backfill ran.

using FluentAssertions;
using MealsEnPlace.Api.Models.Entities;
using MealsEnPlace.Tools.Ingest;

namespace MealsEnPlace.Unit.Tools.Ingest;

public class IngestSummaryTests
{
    // ── Format — backfill ran (live mode) ────────────────────────────────────

    [Fact]
    public void Format_LiveRunWithBackfill_ContainsYesOnBackfillLine()
    {
        // Arrange
        var summary = new IngestSummary
        {
            RecipeReferenceCountBackfilled = true
        };
        summary.StopTimer();

        var options = new IngestOptions { CsvPath = "recipes.csv", DryRun = false };
        var counters = new StreamCounters();

        // Act
        var rendered = summary.Format(options, counters);

        // Assert — the "Ref count backfilled" line must say "yes" when the UPDATE ran
        rendered.Should().Contain("yes");
        rendered.Should().NotContain("skipped (dry run)");
    }

    // ── Format — backfill skipped (dry-run mode) ─────────────────────────────

    [Fact]
    public void Format_DryRun_ContainsSkippedDryRunOnBackfillLine()
    {
        // Arrange — dry-run never writes RecipeIngredient rows, so the UPDATE is skipped
        var summary = new IngestSummary
        {
            RecipeReferenceCountBackfilled = false
        };
        summary.StopTimer();

        var options = new IngestOptions { CsvPath = "recipes.csv", DryRun = true };
        var counters = new StreamCounters();

        // Act
        var rendered = summary.Format(options, counters);

        // Assert
        rendered.Should().Contain("skipped (dry run)");
        rendered.Should().Contain("DRY RUN");
    }

    // ── Format — general structure ────────────────────────────────────────────

    [Fact]
    public void Format_AnyRun_ContainsBackfillHeaderLabel()
    {
        // Arrange
        var summary = new IngestSummary();
        summary.StopTimer();

        var options = new IngestOptions { CsvPath = "data.csv", DryRun = false };
        var counters = new StreamCounters();

        // Act
        var rendered = summary.Format(options, counters);

        // Assert — the label must always appear regardless of whether it ran
        rendered.Should().Contain("Ref count backfilled");
    }

    // ── MEP-063: type distribution and top deferred tokens ───────────────────
    //
    // Scenario: Format prints the resolved unit of measure type distribution
    //   Given an IngestSummary with resolved Count, Volume, and Weight ingredients recorded
    //   When Format is called
    //   Then the output lists Count, Volume, and Weight counts
    //
    // Scenario: Top deferred tokens are ordered by occurrence count
    //   Given the tokens "sprigs" (3 times), "dashes" (1 time), and "bunches" (2 times) are recorded
    //   When Format is called
    //   Then the output lists "sprigs" before "bunches" before "dashes"

    [Fact]
    public void Format_ResolvedTypeCounts_PrintsDistribution()
    {
        // Arrange
        var summary = new IngestSummary { DeterministicallyResolvedIngredients = 6 };
        summary.RecordResolvedUnitOfMeasureType(UnitOfMeasureType.Count);
        summary.RecordResolvedUnitOfMeasureType(UnitOfMeasureType.Volume);
        summary.RecordResolvedUnitOfMeasureType(UnitOfMeasureType.Volume);
        summary.RecordResolvedUnitOfMeasureType(UnitOfMeasureType.Weight);
        summary.RecordResolvedUnitOfMeasureType(UnitOfMeasureType.Weight);
        summary.RecordResolvedUnitOfMeasureType(UnitOfMeasureType.Weight);
        summary.StopTimer();

        var options = new IngestOptions { CsvPath = "data.csv", DryRun = false };

        // Act
        var rendered = summary.Format(options, new StreamCounters());

        // Assert
        summary.ResolvedCountIngredients.Should().Be(1);
        summary.ResolvedVolumeIngredients.Should().Be(2);
        summary.ResolvedWeightIngredients.Should().Be(3);
        rendered.Should().Contain("Count");
        rendered.Should().Contain("Volume");
        rendered.Should().Contain("Weight");
    }

    [Fact]
    public void RecordDeferredUnitToken_MultipleOccurrences_TopTokensOrderedByCount()
    {
        // Arrange
        var summary = new IngestSummary();
        summary.RecordDeferredUnitToken("dashes");
        summary.RecordDeferredUnitToken("sprigs");
        summary.RecordDeferredUnitToken("bunches");
        summary.RecordDeferredUnitToken("sprigs");
        summary.RecordDeferredUnitToken("bunches");
        summary.RecordDeferredUnitToken("sprigs");
        summary.StopTimer();

        var options = new IngestOptions { CsvPath = "data.csv", DryRun = false };

        // Act
        var rendered = summary.Format(options, new StreamCounters());

        // Assert
        var sprigs = rendered.IndexOf("sprigs", StringComparison.Ordinal);
        var bunches = rendered.IndexOf("bunches", StringComparison.Ordinal);
        var dashes = rendered.IndexOf("dashes", StringComparison.Ordinal);
        sprigs.Should().BeGreaterThan(-1);
        bunches.Should().BeGreaterThan(sprigs);
        dashes.Should().BeGreaterThan(bunches);
    }

    // ── MEP-067: re-ingest gate and new counters ──────────────────────────────

    [Fact]
    public void Format_NoBlockedTokensInTopDeferred_PrintsGatePass()
    {
        var summary = new IngestSummary();
        summary.RecordDeferredUnitToken("sprigs");
        summary.StopTimer();

        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        summary.GetReingestGateViolations().Should().BeEmpty();
        rendered.Should().Contain("Re-ingest gate (MEP-067): PASS");
    }

    [Fact]
    public void Format_BlockedTokensInTopDeferred_PrintsGateFailListingThem()
    {
        var summary = new IngestSummary();
        summary.RecordDeferredUnitToken("sticks");
        summary.RecordDeferredUnitToken("NONE");
        summary.RecordDeferredUnitToken("sprigs");
        summary.StopTimer();

        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        summary.GetReingestGateViolations().Should().BeEquivalentTo("sticks", "NONE");
        rendered.Should().Contain("Re-ingest gate (MEP-067): FAIL:");
        rendered.Should().Contain("sticks");
        rendered.Should().Contain("NONE");
    }

    [Fact]
    public void GetReingestGateViolations_BlockedTokenRankedBeyondTopTwentyFive_DoesNotFail()
    {
        var summary = new IngestSummary();
        for (var index = 0; index < IngestConstants.TopDeferredUnitTokenCount; index++)
        {
            for (var repeat = 0; repeat < 10; repeat++)
            {
                summary.RecordDeferredUnitToken($"filler{index:D2}");
            }
        }

        summary.RecordDeferredUnitToken("sticks");

        summary.GetReingestGateViolations().Should().BeEmpty();
    }

    [Fact]
    public void Format_NewCounters_PrintsJunkToTasteAndRecipeResolvedPercentage()
    {
        var summary = new IngestSummary
        {
            JunkIngredientLinesDropped = 7,
            RecipesIngested = 4,
            ToTasteIngredients = 9
        };
        summary.RecordRecipeResolution(true);
        summary.RecordRecipeResolution(true);
        summary.RecordRecipeResolution(true);
        summary.RecordRecipeResolution(false);
        summary.StopTimer();

        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        summary.RecipesFullyResolved.Should().Be(3);
        rendered.Should().Contain("Junk lines dropped:          7");
        rendered.Should().Contain("To-taste (qty 0, ea):        9");
        rendered.Should().Contain("Fully resolved:              3 of 4 (75.0%)");
    }

    // ── Format — NER token normalization counter ──────────────────────────────

    [Fact]
    public void Format_WithNerTokensNormalized_ContainsNormalizedLabelAndCount()
    {
        // Arrange
        var summary = new IngestSummary { NerTokensNormalized = 42 };
        summary.StopTimer();

        var options = new IngestOptions { CsvPath = "data.csv", DryRun = false };
        var counters = new StreamCounters();

        // Act
        var rendered = summary.Format(options, counters);

        // Assert
        rendered.Should().Contain("NER tokens normalized");
        rendered.Should().Contain("42");
    }

    // ── Format — NER token rejection counter ─────────────────────────────────

    [Fact]
    public void Format_WithNerTokensRejected_ContainsRejectedLabelAndCount()
    {
        // Arrange
        var summary = new IngestSummary { NerTokensRejected = 13 };
        summary.StopTimer();

        var options = new IngestOptions { CsvPath = "data.csv", DryRun = false };
        var counters = new StreamCounters();

        // Act
        var rendered = summary.Format(options, counters);

        // Assert
        rendered.Should().Contain("NER tokens rejected");
        rendered.Should().Contain("13");
    }
}
