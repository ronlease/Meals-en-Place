using System.Diagnostics;
using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Mutable counter bag updated during an ingest run. Emitted as a formatted
/// summary block when the run completes.
/// </summary>
internal sealed class IngestSummary
{
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

    public int RecipesIngested { get; set; }

    public int RecipesSkippedByMaxRows { get; set; }

    public int TotalIngredientsProcessed { get; set; }

    public int UnitOfMeasureDeferredToQueue { get; set; }

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

    public void StopTimer() => _stopwatch.Stop();

    public string Format(IngestOptions options, StreamCounters streamCounters)
    {
        var stepsTotal = InstructionStepsRetained + InstructionStepsDropped;
        var retention = stepsTotal == 0 ? 0.0 : InstructionStepsRetained * 100.0 / stepsTotal;

        var unitOfMeasureTotal = DeterministicallyResolvedIngredients + UnitOfMeasureDeferredToQueue;
        var detPercent = unitOfMeasureTotal == 0 ? 0.0 : DeterministicallyResolvedIngredients * 100.0 / unitOfMeasureTotal;
        var deferPercent = unitOfMeasureTotal == 0 ? 0.0 : UnitOfMeasureDeferredToQueue * 100.0 / unitOfMeasureTotal;

        var containerPercent = TotalIngredientsProcessed == 0
            ? 0.0
            : ContainerFlaggedIngredients * 100.0 / TotalIngredientsProcessed;

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
              Batches flushed:             {BatchesFlushed:N0}
              CanonicalIngredients created:{CanonicalIngredientsCreated:N0}

            Ingredients
              Total processed:             {TotalIngredientsProcessed:N0}
              Container-flagged:           {ContainerFlaggedIngredients:N0} ({containerPercent:0.0}%)
              unit of measure resolved deterministic:  {DeterministicallyResolvedIngredients:N0} ({detPercent:0.0}% of non-container)
              unit of measure deferred to review queue:{UnitOfMeasureDeferredToQueue:N0} ({deferPercent:0.0}%)
              No NER match (unlinked):     {IngredientsWithoutNerMatch:N0}

            Instructions
              Steps retained:              {InstructionStepsRetained:N0}
              Steps dropped:               {InstructionStepsDropped:N0}
              Retention rate:              {retention:0.0}%

            Timing
              Elapsed:                     {Elapsed:c}

            """;
    }
}
