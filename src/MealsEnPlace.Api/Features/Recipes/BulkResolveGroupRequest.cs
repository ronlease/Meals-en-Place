namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Request body for the bulk-resolve endpoint. Declares the net weight or
/// volume of one grouped container reference, applied to every unresolved
/// <c>RecipeIngredient</c> row matching the group key
/// (<see cref="CanonicalIngredientId"/>, <see cref="Notes"/>).
/// </summary>
public sealed class BulkResolveGroupRequest
{
    /// <summary>Canonical ingredient id that identifies the group.</summary>
    public Guid CanonicalIngredientId { get; init; }

    /// <summary>
    /// The notes phrase that identifies the group (e.g. "1 can diced tomatoes").
    /// Matched case-insensitively against persisted <c>RecipeIngredient.Notes</c>.
    /// </summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Declared net weight or volume, in the unit identified by <see cref="UomId"/>.</summary>
    public decimal Quantity { get; init; }

    /// <summary>The canonical <c>UnitOfMeasure</c> for <see cref="Quantity"/>.</summary>
    public Guid UomId { get; init; }
}
