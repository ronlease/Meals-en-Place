using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Result of an <see cref="InMemoryUnitOfMeasureResolver.NormalizeOrDefer"/>
/// call. Either resolved to a canonical <c>UnitOfMeasure</c>, or deferred to
/// the review queue. Two further outcomes never queue: a junk line
/// (<see cref="IsJunkLine"/>, no ingredient quantity at all) and a to-taste
/// seasoning (<see cref="IsToTaste"/>, quantity 0 in "ea").
/// </summary>
internal sealed class IngestUnitOfMeasureResolution
{
    /// <summary>The unit token queued for review when deferred; null when resolved.</summary>
    public string? DeferredUnitToken { get; init; }

    /// <summary>True when the line is junk ("None", "-", "to", "or"); never queued, no ingredient row.</summary>
    public bool IsJunkLine { get; init; }

    /// <summary>True when the line is a to-taste seasoning; resolved as quantity 0 in "ea", never queued.</summary>
    public bool IsToTaste { get; init; }

    public decimal Quantity { get; init; }

    public string? UnitOfMeasureAbbreviation { get; init; }

    public Guid? UnitOfMeasureId { get; init; }

    /// <summary>The type of the resolved unit; null when deferred.</summary>
    public UnitOfMeasureType? UnitOfMeasureType { get; init; }

    public bool WasDeferred { get; init; }

    public static IngestUnitOfMeasureResolution Deferred(decimal quantity, string? deferredUnitToken) =>
        new() { DeferredUnitToken = deferredUnitToken, Quantity = quantity, WasDeferred = true };

    /// <summary>Creates the result for a junk line: not deferred, no unit, no queue row.</summary>
    public static IngestUnitOfMeasureResolution JunkLine() =>
        new() { IsJunkLine = true, WasDeferred = false };

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

    /// <summary>Creates the result for a to-taste seasoning: quantity 0 in "ea", never queued.</summary>
    public static IngestUnitOfMeasureResolution ToTaste(Guid eachUnitOfMeasureId, string eachAbbreviation) =>
        new()
        {
            IsToTaste = true,
            Quantity = 0m,
            UnitOfMeasureAbbreviation = eachAbbreviation,
            UnitOfMeasureId = eachUnitOfMeasureId,
            UnitOfMeasureType = Api.Models.Entities.UnitOfMeasureType.Count,
            WasDeferred = false
        };
}
