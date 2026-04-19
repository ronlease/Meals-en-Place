namespace MealsEnPlace.Api.Features.UnitOfMeasureReviewQueue;

/// <summary>
/// Request body for mapping an unresolved unit-of-measure token to a canonical
/// <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure"/>. Creates a new
/// <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasureAlias"/> row so
/// future occurrences of the same token resolve deterministically.
/// </summary>
public sealed class MapTokenToUnitOfMeasureRequest
{
    /// <summary>
    /// When true, the controller inserts the alias even if another alias with
    /// the same text (case-sensitive match) already exists. Required for the
    /// legitimate case-sensitive variants recipe notation uses -- e.g. creating
    /// "T" for Tablespoon when "t" already exists for Teaspoon. Default false.
    /// </summary>
    public bool AllowDuplicateAlias { get; init; }

    /// <summary>
    /// The target <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Id"/>.
    /// </summary>
    public Guid UnitOfMeasureId { get; init; }
}
