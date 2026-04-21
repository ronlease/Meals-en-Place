namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// Centralized constants for the dedup tool. Every magic number or string
/// used by the dedup flow lives here so tuning is a one-line change and
/// every decision has a documented name.
/// </summary>
internal static class DedupConstants
{
    /// <summary>
    /// Number of fold groups processed per database transaction. Each fold
    /// group issues five bulk UPDATEs (RecipeIngredient, InventoryItem,
    /// ShoppingListItem, SeasonalityWindow, ConsumeAuditEntry) + one INSERT
    /// + one DELETE per loser row, so batching keeps transaction size bounded.
    /// </summary>
    public const int FoldGroupBatchSize = 50;

    /// <summary>Dedup completed successfully; summary printed.</summary>
    public const int ExitCodeSuccess = 0;

    /// <summary>Invalid CLI arguments.</summary>
    public const int ExitCodeInvalidArguments = 1;

    /// <summary>
    /// Fatal error during dedup (connection failure, constraint violation,
    /// unexpected IO error).
    /// </summary>
    public const int ExitCodeFatalError = 2;
}
