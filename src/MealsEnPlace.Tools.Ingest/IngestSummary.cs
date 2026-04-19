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

    public int CanonicalIngredientsCreated { get; set; }

    public int ContainerFlaggedIngredients { get; set; }

    public int DeterministicallyResolvedIngredients { get; set; }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public int InstructionStepsDropped { get; set; }

    public int InstructionStepsRetained { get; set; }

    public int RecipesIngested { get; set; }

    public int RecipesSkippedByMaxRows { get; set; }

    public int TotalIngredientsProcessed { get; set; }

    public int UomDeferredToQueue { get; set; }

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

        var uomTotal = DeterministicallyResolvedIngredients + UomDeferredToQueue;
        var detPercent = uomTotal == 0 ? 0.0 : DeterministicallyResolvedIngredients * 100.0 / uomTotal;
        var deferPercent = uomTotal == 0 ? 0.0 : UomDeferredToQueue * 100.0 / uomTotal;

        var containerPercent = TotalIngredientsProcessed == 0
            ? 0.0
            : ContainerFlaggedIngredients * 100.0 / TotalIngredientsProcessed;

        return $"""

            MealsEnPlace.Tools.Ingest summary
            =================================
            Mode:                          {(options.DryRun ? "DRY RUN (no database writes)" : "LIVE")}
            Input:                         {options.CsvPath}
            {(options.MaxRows is { } max ? $"Max rows cap:                  {max:N0}" : "Max rows cap:                  (none)")}

            Stream
              Rows read:                   {streamCounters.TotalRowsRead:N0}
              Skipped (source=Recipes1M):  {streamCounters.SkippedRecipes1M:N0}
              Skipped (malformed JSON):    {streamCounters.MalformedRowsSkipped:N0}
              Skipped (--max-rows cap):    {RecipesSkippedByMaxRows:N0}

            Recipes
              Ingested:                    {RecipesIngested:N0}
              CanonicalIngredients created:{CanonicalIngredientsCreated:N0}

            Ingredients
              Total processed:             {TotalIngredientsProcessed:N0}
              Container-flagged:           {ContainerFlaggedIngredients:N0} ({containerPercent:0.0}%)
              UOM resolved deterministic:  {DeterministicallyResolvedIngredients:N0} ({detPercent:0.0}% of non-container)
              UOM deferred to review queue:{UomDeferredToQueue:N0} ({deferPercent:0.0}%)

            Instructions
              Steps retained:              {InstructionStepsRetained:N0}
              Steps dropped:               {InstructionStepsDropped:N0}
              Retention rate:              {retention:0.0}%

            Timing
              Elapsed:                     {Elapsed:c}

            """;
    }
}
