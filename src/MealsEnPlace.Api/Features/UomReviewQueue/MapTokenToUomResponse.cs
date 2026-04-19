namespace MealsEnPlace.Api.Features.UomReviewQueue;

/// <summary>
/// Response body for a successful <see cref="UomReviewQueueController.Map"/> call.
/// </summary>
public sealed class MapTokenToUomResponse
{
    /// <summary>The id of the newly-created <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasureAlias"/>.</summary>
    public Guid AliasId { get; init; }

    /// <summary>The alias text that was persisted (matches the original queue-row UnitToken).</summary>
    public string AliasText { get; init; } = string.Empty;

    /// <summary>The canonical <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Id"/> the alias maps to.</summary>
    public Guid UnitOfMeasureId { get; init; }
}
