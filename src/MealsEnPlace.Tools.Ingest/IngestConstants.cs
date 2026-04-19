using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Centralized constants for the ingest tool. Every magic number or string
/// used by ingest flow lives here so tuning is a one-line change and every
/// decision has a documented name.
/// </summary>
internal static class IngestConstants
{
    // ── Batching ────────────────────────────────────────────────────────────

    /// <summary>
    /// Number of recipes accumulated in the EF Core change tracker before
    /// SaveChangesAsync is called and the tracker is cleared. Balances the
    /// per-batch working set (tracker size) against SaveChanges overhead.
    /// </summary>
    public const int RecipeBatchSize = 100;

    /// <summary>
    /// Recipes processed between stdout progress updates during a long run.
    /// </summary>
    public const int ProgressLoggingIntervalRecipes = 1_000;

    /// <summary>
    /// Initial capacity for the <c>CanonicalIngredient</c> dedup dictionary.
    /// Projected 5k-15k unique NER tokens across 1.64M recipes per MEP-025
    /// measurement. Pre-sizing avoids rehashing as ingest progresses.
    /// </summary>
    public const int CanonicalIngredientDedupCacheInitialCapacity = 20_000;

    /// <summary>
    /// Initial capacity for the <c>UnresolvedUnitOfMeasureToken</c> dedup
    /// dictionary. Unique token counts in ingest runs are typically in the
    /// low thousands.
    /// </summary>
    public const int UnresolvedTokenDedupCacheInitialCapacity = 5_000;

    // ── Exit codes ─────────────────────────────────────────────────────────

    /// <summary>Ingest completed successfully; summary printed.</summary>
    public const int ExitCodeSuccess = 0;

    /// <summary>Invalid CLI arguments or CSV file not found.</summary>
    public const int ExitCodeInvalidArguments = 1;

    /// <summary>
    /// Fatal error during ingest (connection failure, DB constraint violation,
    /// unexpected IO error).
    /// </summary>
    public const int ExitCodeFatalError = 2;

    // ── Defaults for newly-created CanonicalIngredients from NER ──────────

    /// <summary>
    /// Default UnitOfMeasure.Abbreviation assigned to CanonicalIngredient
    /// rows created from NER tokens. "ea" (Each) is a safe neutral default;
    /// the user can adjust per ingredient after ingest if needed.
    /// </summary>
    public const string DefaultCanonicalIngredientUnitOfMeasureAbbreviation = "ea";

    /// <summary>
    /// Default category assigned to CanonicalIngredient rows created from
    /// NER tokens. "Other" keeps recipes usable without forcing a guess;
    /// a future categorization pass (possibly Claude-backed) can refine.
    /// </summary>
    public const IngredientCategory DefaultCanonicalIngredientCategory = IngredientCategory.Other;

    // ── Recipe defaults (Kaggle dataset does not provide these) ───────────

    /// <summary>
    /// ServingCount assigned to imported recipes. The dataset has no serving
    /// information, so 1 is used as a neutral non-zero placeholder the user
    /// can edit later.
    /// </summary>
    public const int DefaultServingCount = 1;

    /// <summary>
    /// CuisineType for imported recipes. Dataset does not expose a cuisine
    /// field, so an empty string is persisted and the user (or a later
    /// classification pass) can populate it.
    /// </summary>
    public const string DefaultCuisineType = "";

    /// <summary>
    /// Separator used to join prose-filter-retained instruction steps into
    /// the single <c>Recipe.Instructions</c> string column.
    /// </summary>
    public const string InstructionStepSeparator = "\n\n";

    // ── Ingest mode UOM context ─────────────────────────────────────────────

    /// <summary>
    /// Generic ingredient-name context string passed to the review-queue
    /// writer when the raw ingredient cannot be heuristically linked to any
    /// NER token. Shows up in the queue's SampleIngredientContext column.
    /// </summary>
    public const string UnlinkedIngredientContext = "(unlinked)";
}
