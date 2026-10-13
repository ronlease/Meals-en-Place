using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Result of an <see cref="InMemoryUnitOfMeasureResolver.NormalizeOrDefer"/>
/// call. Either resolved to a canonical <c>UnitOfMeasure</c>, or deferred to
/// the review queue.
/// </summary>
internal sealed class IngestUnitOfMeasureResolution
{
    /// <summary>The unit token queued for review when deferred; null when resolved.</summary>
    public string? DeferredUnitToken { get; init; }

    public decimal Quantity { get; init; }

    public string? UnitOfMeasureAbbreviation { get; init; }

    public Guid? UnitOfMeasureId { get; init; }

    /// <summary>The type of the resolved unit; null when deferred.</summary>
    public UnitOfMeasureType? UnitOfMeasureType { get; init; }

    public bool WasDeferred { get; init; }

    public static IngestUnitOfMeasureResolution Deferred(decimal quantity, string? deferredUnitToken) =>
        new() { DeferredUnitToken = deferredUnitToken, Quantity = quantity, WasDeferred = true };

    public static IngestUnitOfMeasureResolution Resolved(
        decimal quantity,
        Guid unitOfMeasureId,
        string unitOfMeasureAbbreviation,
        UnitOfMeasureType unitOfMeasureType) =>
        new()
        {
            Quantity = quantity,
            UnitOfMeasureAbbreviation = unitOfMeasureAbbreviation,
            UnitOfMeasureId = unitOfMeasureId,
            UnitOfMeasureType = unitOfMeasureType,
            WasDeferred = false
        };
}
