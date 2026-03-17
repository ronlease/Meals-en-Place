using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.SeasonalProduce;

/// <summary>
/// Response DTO for a produce item with its peak season window.
/// </summary>
public record SeasonalProduceResponse
{
    /// <summary>Canonical ingredient ID.</summary>
    public required Guid IngredientId { get; init; }

    /// <summary>Produce name.</summary>
    public required string Name { get; init; }

    /// <summary>End month of peak season.</summary>
    public required Month PeakSeasonEnd { get; init; }

    /// <summary>Start month of peak season.</summary>
    public required Month PeakSeasonStart { get; init; }

    /// <summary>USDA zone this window applies to.</summary>
    public required string UsdaZone { get; init; }
}
