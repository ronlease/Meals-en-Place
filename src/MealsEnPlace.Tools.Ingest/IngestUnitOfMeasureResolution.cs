namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Result of an <see cref="InMemoryUnitOfMeasureResolver.NormalizeOrDefer"/>
/// call. Either resolved to a canonical <c>UnitOfMeasure</c>, or deferred to
/// the review queue.
/// </summary>
internal sealed class IngestUnitOfMeasureResolution
{
    public decimal Quantity { get; init; }

    public string? UnitOfMeasureAbbreviation { get; init; }

    public Guid? UnitOfMeasureId { get; init; }

    public bool WasDeferred { get; init; }

    public static IngestUnitOfMeasureResolution Deferred(decimal quantity) =>
        new() { Quantity = quantity, WasDeferred = true };

    public static IngestUnitOfMeasureResolution Resolved(
        decimal quantity, Guid unitOfMeasureId, string unitOfMeasureAbbreviation) =>
        new()
        {
            Quantity = quantity,
            UnitOfMeasureAbbreviation = unitOfMeasureAbbreviation,
            UnitOfMeasureId = unitOfMeasureId,
            WasDeferred = false
        };
}
