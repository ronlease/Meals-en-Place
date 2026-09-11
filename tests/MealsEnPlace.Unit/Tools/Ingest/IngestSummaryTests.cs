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
}
