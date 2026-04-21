using System.Diagnostics;

namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// Mutable counter bag updated during a dedup run. Emitted as a formatted
/// summary block when the run completes.
/// </summary>
internal sealed class DedupSummary
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public int AliasRowsWritten { get; set; }

    public int CanonicalIngredientsLoaded { get; set; }

    public int ConsumeAuditEntryFksReassigned { get; set; }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public int FoldGroupCount { get; set; }

    public int InventoryItemFksReassigned { get; set; }

    public int LoserRowsDeleted { get; set; }

    public int RecipeIngredientFksReassigned { get; set; }

    public int SeasonalityWindowFksReassigned { get; set; }

    public int ShoppingListItemFksReassigned { get; set; }

    public int TotalFksReassigned =>
        ConsumeAuditEntryFksReassigned
        + InventoryItemFksReassigned
        + RecipeIngredientFksReassigned
        + SeasonalityWindowFksReassigned
        + ShoppingListItemFksReassigned;

    public void StopTimer() => _stopwatch.Stop();

    public string Format(DedupOptions options) =>
        $"""

            MealsEnPlace.Tools.Dedup summary
            ================================
            Mode:                          {(options.DryRun ? "DRY RUN (no database writes)" : "LIVE")}

            Input
              CanonicalIngredients loaded: {CanonicalIngredientsLoaded:N0}

            Fold plan
              Fold groups:                 {FoldGroupCount:N0}
              Loser rows {(options.DryRun ? "to delete" : "deleted")}:         {LoserRowsDeleted:N0}
              Alias rows {(options.DryRun ? "to write" : "written")}:          {AliasRowsWritten:N0}

            FK reassignment ({(options.DryRun ? "projected" : "applied")})
              RecipeIngredient:            {RecipeIngredientFksReassigned:N0}
              InventoryItem:               {InventoryItemFksReassigned:N0}
              ShoppingListItem:            {ShoppingListItemFksReassigned:N0}
              SeasonalityWindow:           {SeasonalityWindowFksReassigned:N0}
              ConsumeAuditEntry:           {ConsumeAuditEntryFksReassigned:N0}
              Total:                       {TotalFksReassigned:N0}

            Timing
              Elapsed:                     {Elapsed:c}

            """;
}
