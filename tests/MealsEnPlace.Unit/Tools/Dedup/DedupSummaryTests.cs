// Feature: DedupSummary formatter and totals
//
// Scenario: TotalFksReassigned sums the five per-table counters
// Scenario: Format renders all counters, the DRY RUN banner, and elapsed timing
// Scenario: Format's LIVE banner appears when DryRun is false

using FluentAssertions;
using MealsEnPlace.Tools.Dedup;

namespace MealsEnPlace.Unit.Tools.Dedup;

public class DedupSummaryTests
{
    [Fact]
    public void TotalFksReassigned_SumsPerTableCounters()
    {
        var summary = new DedupSummary
        {
            RecipeIngredientFksReassigned = 10,
            InventoryItemFksReassigned = 2,
            ShoppingListItemFksReassigned = 3,
            SeasonalityWindowFksReassigned = 1,
            ConsumeAuditEntryFksReassigned = 4
        };

        summary.TotalFksReassigned.Should().Be(20);
    }

    [Fact]
    public void Format_DryRun_IncludesDryRunBannerAndCounters()
    {
        var summary = new DedupSummary
        {
            CanonicalIngredientsLoaded = 100,
            FoldGroupCount = 7,
            LoserRowsDeleted = 15,
            AliasRowsWritten = 15,
            RecipeIngredientFksReassigned = 42
        };
        summary.StopTimer();

        var rendered = summary.Format(new DedupOptions { DryRun = true });

        rendered.Should().Contain("DRY RUN");
        rendered.Should().Contain("100");
        rendered.Should().Contain("7");
        rendered.Should().Contain("42");
    }

    [Fact]
    public void Format_LiveRun_IncludesLiveBanner()
    {
        var summary = new DedupSummary();
        summary.StopTimer();

        var rendered = summary.Format(new DedupOptions { DryRun = false });

        rendered.Should().Contain("LIVE");
        rendered.Should().NotContain("DRY RUN");
    }
}
