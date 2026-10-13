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
    /// Tallies one recipe toward the fully-resolved share. Pass true when the
    /// recipe has no deferred and no container-flagged ingredient.
    /// </summary>
    public void RecordRecipeResolution(bool fullyResolved)
    {
        if (fullyResolved)
        {
            RecipesFullyResolved++;
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
              Batches flushed:             {BatchesFlushed:N0}
              CanonicalIngredients created:{CanonicalIngredientsCreated:N0}
              Ref count backfilled:        {(RecipeReferenceCountBackfilled ? "yes" : "skipped (dry run)")}

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

    private IEnumerable<KeyValuePair<string, int>> GetTopDeferredUnitTokens() =>
        _deferredUnitTokenCounts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Take(IngestConstants.TopDeferredUnitTokenCount);
}
