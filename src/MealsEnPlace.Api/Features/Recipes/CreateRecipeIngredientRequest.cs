namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Describes a single ingredient line when creating a recipe manually.
/// </summary>
public sealed class CreateRecipeIngredientRequest
{
    /// <summary>The canonical ingredient to associate with this line.</summary>
    public Guid CanonicalIngredientId { get; init; }

    /// <summary>
    /// Original text when the measure is a container reference (e.g., "1 can chopped tomatoes").
    /// Preserved in RecipeIngredient.Notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>Quantity in the unit specified by <see cref="UnitOfMeasureId"/>.</summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// The unit of measure identifier. Null signals a container reference that must be resolved
    /// before this recipe participates in matching.
    /// </summary>
    public Guid? UnitOfMeasureId { get; init; }
}
