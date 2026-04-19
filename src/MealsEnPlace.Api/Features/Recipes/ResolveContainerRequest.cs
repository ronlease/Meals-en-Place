namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Request body for resolving a container reference on a recipe ingredient.
/// The user declares the net weight or volume of the container so that the
/// ingredient can participate in recipe matching math.
/// </summary>
public sealed class ResolveContainerRequest
{
    /// <summary>
    /// The declared net quantity of the container (e.g., 14.5 for a 14.5 oz can).
    /// Must be greater than zero.
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// The id of the <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure"/> that
    /// applies to <see cref="Quantity"/> (e.g., the id for "oz" or "ml").
    /// </summary>
    public Guid UnitOfMeasureId { get; init; }
}
