using System.Diagnostics;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Mutable counter bag updated during an ingest run. Emitted as a formatted
/// summary block when the run completes.
/// </summary>
internal sealed class IngestSummary
{
    private readonly Dictionary<string, int> _containerKeywordRecipeCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _containerKeywordRowCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _deferredUnitTokenCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    /// <summary>
    /// True once the ingest loop is short-circuited by the <c>--max-rows</c>
    /// cap. Used only by the summary formatter to adjust stream-counter
    /// interpretation so callers don't misread partial counts as a full pass.
    /// </summary>
    public bool StreamTerminatedByMaxRowsCap { get; set; }

    public int BatchesFlushed { get; set; }

    public int CanonicalIngredientsCreated { get; set; }

    public int ContainerFlaggedIngredients { get; set; }

    public int DeterministicallyResolvedIngredients { get; set; }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public int IngredientsWithoutNerMatch { get; set; }

    public int InstructionStepsDropped { get; set; }

    public int InstructionStepsRetained { get; set; }

    /// <summary>Count of junk ingredient lines ("None", "-", "to", "or") dropped without a RecipeIngredient row.</summary>
    public int JunkIngredientLinesDropped { get; set; }

    /// <summary>
    /// Count of NER tokens whose normalized value differed from the original
    /// token string. Informational: shows how much Kaggle noise was cleaned up.
    /// </summary>
    public int NerTokensNormalized { get; set; }

    /// <summary>
    /// Count of NER tokens that were rejected (empty after cleanup, no letters,
    /// or stopwords only) and therefore produced no CanonicalIngredient row.
    /// </summary>
    public int NerTokensRejected { get; set; }

    public bool RecipeReferenceCountBackfilled { get; set; }

    /// <summary>Recipes blocked by at least one container-flagged and at least one deferred ingredient.</summary>
    public int RecipesBlockedByBoth { get; private set; }

    /// <summary>Recipes blocked only by container-flagged ingredients (no deferred ingredient).</summary>
    public int RecipesBlockedOnlyByContainer { get; private set; }

    /// <summary>Recipes blocked only by deferred, non-container ingredients (no container-flagged ingredient).</summary>
    public int RecipesBlockedOnlyByDeferred { get; private set; }

    /// <summary>
    /// Recipes with no deferred and no container-flagged ingredient: the dry-run
    /// estimate of the MEP-067 fully-resolved share.
    /// </summary>
    public int RecipesFullyResolved { get; private set; }

    public int RecipesIngested { get; set; }

    public int RecipesSkippedByMaxRows { get; set; }

    public int ResolvedArbitraryIngredients { get; set; }

    public int ResolvedCountIngredients { get; set; }

    public int ResolvedVolumeIngredients { get; set; }

    public int ResolvedWeightIngredients { get; set; }

    public int TotalIngredientsProcessed { get; set; }

    /// <summary>Count of to-taste seasoning lines stored as quantity 0 in "ea".</summary>
    public int ToTasteIngredients { get; set; }

    public int UnitOfMeasureDeferredToQueue { get; set; }

    /// <summary>Tallies a unit token that was deferred to the review queue.</summary>
    public void RecordDeferredUnitToken(string? unitToken)
    {
        if (string.IsNullOrWhiteSpace(unitToken))
        {
            return;
        }

        _deferredUnitTokenCounts[unitToken] = _deferredUnitTokenCounts.GetValueOrDefault(unitToken) + 1;
    }

    public void RecordProseFilter(ProseFilterResult result)
    {
        if (result.Retained)
        {
            InstructionStepsRetained++;
        }
        else
        {
            InstructionStepsDropped++;
        }
    }

    /// <summary>
    /// Tallies one recipe into exactly one of four mutually exclusive buckets:
    /// fully resolved, blocked only by container-flagged ingredients, blocked
    /// only by deferred ingredients, or blocked by both. A container-flagged
    /// row and a deferred row both have a null unit of measure and an
    /// unresolved container flag, so the ingest loop must tell them apart.
    /// </summary>
    /// <param name="containerKeywords">
    /// One matched container keyword per container-flagged ingredient row of the recipe.
    /// </param>
    /// <param name="deferredIngredientCount">
    /// Count of non-container ingredient rows of the recipe whose unit of measure was deferred.
    /// </param>
    public void RecordRecipeResolution(IReadOnlyCollection<string> containerKeywords, int deferredIngredientCount)
    {
        var hasContainer = containerKeywords.Count > 0;
        var hasDeferred = deferredIngredientCount > 0;

        foreach (var keyword in containerKeywords)
        {
            _containerKeywordRowCounts[keyword] = _containerKeywordRowCounts.GetValueOrDefault(keyword) + 1;
        }

        if (!hasContainer && !hasDeferred)
        {
            RecipesFullyResolved++;
        }
        else if (hasContainer && hasDeferred)
        {
            RecipesBlockedByBoth++;
        }
        else if (hasContainer)
        {
            RecipesBlockedOnlyByContainer++;
            foreach (var keyword in containerKeywords.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _containerKeywordRecipeCounts[keyword] = _containerKeywordRecipeCounts.GetValueOrDefault(keyword) + 1;
            }
        }
        else
        {
            RecipesBlockedOnlyByDeferred++;
        }
    }

    /// <summary>Tallies a deterministically resolved ingredient by unit of measure type.</summary>
    public void RecordResolvedUnitOfMeasureType(UnitOfMeasureType unitOfMeasureType)
    {
        switch (unitOfMeasureType)
        {
            case UnitOfMeasureType.Count:
                ResolvedCountIngredients++;
                break;
            case UnitOfMeasureType.Volume:
                ResolvedVolumeIngredients++;
                break;
            case UnitOfMeasureType.Weight:
                ResolvedWeightIngredients++;
                break;
            default:
                ResolvedArbitraryIngredients++;
                break;
        }
    }

    /// <summary>
    /// Returns the MEP-067 re-ingest gate blocked tokens that appear in the top
    /// <see cref="IngestConstants.TopDeferredUnitTokenCount"/> deferred unit tokens
    /// (case-insensitive). Empty means the gate passes.
    /// </summary>
    public IReadOnlyList<string> GetReingestGateViolations()
    {
        var blocked = new HashSet<string>(
            IngestConstants.ReingestGateBlockedUnitTokens, StringComparer.OrdinalIgnoreCase);

        return GetTopDeferredUnitTokens()
            .Where(kvp => blocked.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public void StopTimer() => _stopwatch.Stop();

    public string Format(IngestOptions options, StreamCounters streamCounters)
    {
        var stepsTotal = InstructionStepsRetained + InstructionStepsDropped;
        var retention = stepsTotal == 0 ? 0.0 : InstructionStepsRetained * 100.0 / stepsTotal;

        var unitOfMeasureTotal = DeterministicallyResolvedIngredients + UnitOfMeasureDeferredToQueue;
        var detPercent = unitOfMeasureTotal == 0 ? 0.0 : DeterministicallyResolvedIngredients * 100.0 / unitOfMeasureTotal;
        var deferPercent = unitOfMeasureTotal == 0 ? 0.0 : UnitOfMeasureDeferredToQueue * 100.0 / unitOfMeasureTotal;

        var resolvedTotal = DeterministicallyResolvedIngredients;
        var countPercent = resolvedTotal == 0 ? 0.0 : ResolvedCountIngredients * 100.0 / resolvedTotal;
        var volumePercent = resolvedTotal == 0 ? 0.0 : ResolvedVolumeIngredients * 100.0 / resolvedTotal;
        var weightPercent = resolvedTotal == 0 ? 0.0 : ResolvedWeightIngredients * 100.0 / resolvedTotal;
        var arbitraryPercent = resolvedTotal == 0 ? 0.0 : ResolvedArbitraryIngredients * 100.0 / resolvedTotal;

        var topDeferred = string.Join(
            Environment.NewLine,
            GetTopDeferredUnitTokens()
                .Select(kvp => $"  {kvp.Value,10:N0}  {kvp.Key}"));
        if (topDeferred.Length == 0)
        {
            topDeferred = "  (none)";
        }

        var containerPercent = TotalIngredientsProcessed == 0
            ? 0.0
            : ContainerFlaggedIngredients * 100.0 / TotalIngredientsProcessed;

        var violations = GetReingestGateViolations();
        var gateLine = violations.Count == 0
            ? "PASS"
            : $"FAIL: {string.Join(", ", violations)}";

        var recipesResolvedPercent = RecipesIngested == 0
            ? 0.0
            : RecipesFullyResolved * 100.0 / RecipesIngested;

        string Percent(int count) => RecipesIngested == 0 ? "0.0" : $"{count * 100.0 / RecipesIngested:0.0}";

        static string FormatTable(IEnumerable<KeyValuePair<string, int>> entries)
        {
            var lines = entries.Select(kvp => $"  {kvp.Value,10:N0}  {kvp.Key}").ToList();
            return lines.Count == 0 ? "  (none)" : string.Join(Environment.NewLine, lines);
        }

        var topContainerByRecipe = FormatTable(GetTopContainerKeywords(_containerKeywordRecipeCounts));
        var topContainerByRow = FormatTable(GetTopContainerKeywords(_containerKeywordRowCounts));

        var capNote = StreamTerminatedByMaxRowsCap
            ? " (stream terminated early by --max-rows cap)"
            : string.Empty;

        return $"""

            MealsEnPlace.Tools.Ingest summary
            =================================
            Mode:                          {(options.DryRun ? "DRY RUN (no database writes)" : "LIVE")}
            Input:                         {options.CsvPath}
            {(options.MaxRows is { } max ? $"Max rows cap:                  {max:N0}" : "Max rows cap:                  (none)")}

            Stream
              Rows read:                   {streamCounters.TotalRowsRead:N0}{capNote}
              Skipped (source=Recipes1M):  {streamCounters.SkippedRecipes1M:N0}
              Skipped (malformed JSON):    {streamCounters.MalformedRowsSkipped:N0}

            Recipes
              Ingested:                    {RecipesIngested:N0}
              Fully resolved:              {RecipesFullyResolved:N0} of {RecipesIngested:N0} ({recipesResolvedPercent:0.0}%)
              Blocked only by container:   {RecipesBlockedOnlyByContainer:N0} ({Percent(RecipesBlockedOnlyByContainer)}%)
              Blocked only by deferred:    {RecipesBlockedOnlyByDeferred:N0} ({Percent(RecipesBlockedOnlyByDeferred)}%)
              Blocked by both:             {RecipesBlockedByBoth:N0} ({Percent(RecipesBlockedByBoth)}%)
              Batches flushed:             {BatchesFlushed:N0}
              CanonicalIngredients created:{CanonicalIngredientsCreated:N0}
              Ref count backfilled:        {(RecipeReferenceCountBackfilled ? "yes" : "skipped (dry run)")}

            Top {IngestConstants.TopContainerKeywordCount} container keywords (recipes blocked only by container, one count per recipe)
            {topContainerByRecipe}

            Top {IngestConstants.TopContainerKeywordCount} container keywords (all container-flagged ingredient rows)
            {topContainerByRow}

            Ingredients
              NER tokens normalized:       {NerTokensNormalized:N0}
              NER tokens rejected:         {NerTokensRejected:N0}
              Total processed:             {TotalIngredientsProcessed:N0}
              Junk lines dropped:          {JunkIngredientLinesDropped:N0}
              To-taste (qty 0, ea):        {ToTasteIngredients:N0}
              Container-flagged:           {ContainerFlaggedIngredients:N0} ({containerPercent:0.0}%)
              Unit of measure resolved deterministic:  {DeterministicallyResolvedIngredients:N0} ({detPercent:0.0}% of non-container)
              Unit of measure deferred to review queue:{UnitOfMeasureDeferredToQueue:N0} ({deferPercent:0.0}%)
              No NER match (unlinked):     {IngredientsWithoutNerMatch:N0}

            Resolved by unit of measure type
              Count:                       {ResolvedCountIngredients:N0} ({countPercent:0.0}%)
              Volume:                      {ResolvedVolumeIngredients:N0} ({volumePercent:0.0}%)
              Weight:                      {ResolvedWeightIngredients:N0} ({weightPercent:0.0}%)
              Arbitrary:                   {ResolvedArbitraryIngredients:N0} ({arbitraryPercent:0.0}%)

            Top {IngestConstants.TopDeferredUnitTokenCount} deferred unit tokens
            {topDeferred}

            Re-ingest gate (MEP-067): {gateLine}

            Instructions
              Steps retained:              {InstructionStepsRetained:N0}
              Steps dropped:               {InstructionStepsDropped:N0}
              Retention rate:              {retention:0.0}%

            Timing
              Elapsed:                     {Elapsed:c}

            """;
    }

    private static IEnumerable<KeyValuePair<string, int>> GetTopContainerKeywords(Dictionary<string, int> counts) =>
        counts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Take(IngestConstants.TopContainerKeywordCount);

    private IEnumerable<KeyValuePair<string, int>> GetTopDeferredUnitTokens() =>
        _deferredUnitTokenCounts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Take(IngestConstants.TopDeferredUnitTokenCount);
}
