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
        summary.RecordRecipeResolution([], 0);
        summary.RecordRecipeResolution([], 0);
        summary.RecordRecipeResolution([], 0);
        summary.RecordRecipeResolution(["can"], 0);
        summary.StopTimer();

        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        summary.RecipesFullyResolved.Should().Be(3);
        rendered.Should().Contain("Junk lines dropped:          7");
        rendered.Should().Contain("To-taste (qty 0, ea):        9");
        rendered.Should().Contain("Fully resolved:              3 of 4 (75.0%)");
    }

    // ── Recipe blocker breakdown ──────────────────────────────────────────────

    [Fact]
    public void RecordRecipeResolution_MixedRecipes_BucketsAreMutuallyExclusiveAndSumToTotal()
    {
        // Given recipes covering every bucket
        var summary = new IngestSummary();

        // When each recipe is recorded
        summary.RecordRecipeResolution([], 0);
        summary.RecordRecipeResolution(["can"], 0);
        summary.RecordRecipeResolution(["jar", "jar"], 0);
        summary.RecordRecipeResolution([], 3);
        summary.RecordRecipeResolution(["bag"], 1);
        summary.RecipesIngested = 5;

        // Then each recipe lands in exactly one bucket
        summary.RecipesFullyResolved.Should().Be(1);
        summary.RecipesBlockedOnlyByContainer.Should().Be(2);
        summary.RecipesBlockedOnlyByDeferred.Should().Be(1);
        summary.RecipesBlockedByBoth.Should().Be(1);
        (summary.RecipesFullyResolved + summary.RecipesBlockedOnlyByContainer
            + summary.RecipesBlockedOnlyByDeferred + summary.RecipesBlockedByBoth)
            .Should().Be(summary.RecipesIngested);
    }

    [Fact]
    public void Format_ContainerBlockedRecipes_PrintsBucketPercentagesAndKeywordTables()
    {
        // Given recipes blocked by containers, with a repeated keyword in one recipe
        var summary = new IngestSummary { RecipesIngested = 4 };
        summary.RecordRecipeResolution([], 0);
        summary.RecordRecipeResolution(["can", "can", "jar"], 0);
        summary.RecordRecipeResolution(["can"], 0);
        summary.RecordRecipeResolution(["bag"], 2);
        summary.StopTimer();

        // When the summary is formatted
        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        // Then buckets, percentages and both keyword tables are present
        rendered.Should().Contain("Fully resolved:              1 of 4 (25.0%)");
        rendered.Should().Contain("Blocked only by container:   2 (50.0%)");
        rendered.Should().Contain("Blocked only by deferred:    0 (0.0%)");
        rendered.Should().Contain("Blocked by both:             1 (25.0%)");

        var byRecipe = rendered.Split("recipes blocked only by container, one count per recipe")[1]
            .Split("all container-flagged ingredient rows")[0];
        byRecipe.Should().Contain("2  can");
        byRecipe.Should().Contain("1  jar");
        byRecipe.Should().NotContain("bag");

        var byRow = rendered.Split("all container-flagged ingredient rows")[1];
        byRow.Should().Contain("3  can");
        byRow.Should().Contain("1  jar");
        byRow.Should().Contain("1  bag");
    }

    [Fact]
    public void Format_NoContainerFlags_PrintsNoneForKeywordTables()
    {
        var summary = new IngestSummary();
        summary.StopTimer();

        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        rendered.Should().Contain("Blocked only by container:   0 (0.0%)");
        rendered.Should().Contain("(none)");
    }

    [Fact]
    public void Format_ManyKeywords_LimitsTablesToTopFifteen()
    {
        var summary = new IngestSummary();
        for (var index = 0; index < 20; index++)
        {
            summary.RecordRecipeResolution([$"keyword{index:D2}"], 0);
        }

        summary.StopTimer();

        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        rendered.Should().Contain("keyword14");
        rendered.Should().NotContain("keyword15");
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

    // ── Stated-size diagnostic (hypothetical) ─────────────────────────────────

    [Fact]
    public void RecordRecipeResolution_StatedSizeFlags_CountsRowsAndHypotheticalRecipes()
    {
        // Given recipes: fully resolved; all container rows stated; one row unstated;
        // all stated but also deferred; no flags supplied
        var summary = new IngestSummary();

        // When recorded
        summary.RecordRecipeResolution([], 0);
        summary.RecordRecipeResolution(["can", "jar"], 0, [true, true]);
        summary.RecordRecipeResolution(["can", "can"], 0, [true, false]);
        summary.RecordRecipeResolution(["bag"], 2, [true]);
        summary.RecordRecipeResolution(["box"], 0);

        // Then rows and the hypothetical bucket are counted
        summary.ContainerRowsWithStatedSize.Should().Be(4);
        summary.ContainerRowsWithoutStatedSize.Should().Be(2);
        summary.RecipesBlockedOnlyByContainerAllStatedSize.Should().Be(1);
        summary.RecipesBlockedOnlyByContainer.Should().Be(3);
    }

    [Fact]
    public void RecordRecipeResolution_MisalignedStatedSizeFlags_Throws()
    {
        var summary = new IngestSummary();

        var act = () => summary.RecordRecipeResolution(["can"], 0, [true, false]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_StatedSizeDiagnostic_PrintsRowAndRecipeBucketsAndHypotheticalShare()
    {
        // Given 4 recipes: 1 resolved, 1 container-only all stated, 1 container-only partial, 1 both
        var summary = new IngestSummary { RecipesIngested = 4 };
        summary.RecordRecipeResolution([], 0);
        summary.RecordRecipeResolution(["can", "jar"], 0, [true, true]);
        summary.RecordRecipeResolution(["can", "can"], 0, [true, false]);
        summary.RecordRecipeResolution(["bag"], 1, [false]);
        summary.StopTimer();

        // When formatted
        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        // Then 3 of 5 rows state a size, 1 recipe (25.0%) would flip, hypothetical share is 2 of 4
        rendered.Should().Contain("Container rows, stated size:     3 of 5 (60.0%)");
        rendered.Should().Contain("Container rows, no stated size:  2 of 5 (40.0%)");
        rendered.Should().Contain("1 (25.0% of all recipes)");
        rendered.Should().Contain("2 of 4 (50.0%)");
        rendered.Should().MatchRegex(@"2 /\s+3\s+\(\s*66\.7%\)\s+can");
        rendered.Should().MatchRegex(@"1 /\s+1\s+\(\s*100\.0%\)\s+jar");
        rendered.Should().Contain("DIAGNOSTIC (hypothetical");
    }

    [Fact]
    public void Format_NoContainerRows_PrintsZeroStatedSizeDiagnostic()
    {
        var summary = new IngestSummary();
        summary.StopTimer();

        var rendered = summary.Format(new IngestOptions { CsvPath = "data.csv" }, new StreamCounters());

        rendered.Should().Contain("Container rows, stated size:     0 of 0 (0.0%)");
    }
}
