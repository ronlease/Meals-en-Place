namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Represents a single recipe ingredient that has an unresolved container reference.
/// The <see cref="Notes"/> field preserves the original measure string from import
/// (e.g., "1 can chopped tomatoes") so that the user knows what to resolve.
/// </summary>
public sealed class UnresolvedIngredientResponse
{
    /// <summary>The canonical ingredient name (e.g., "Diced Tomatoes").</summary>
    public string CanonicalIngredientName { get; init; } = string.Empty;

    /// <summary>Primary key of the <see cref="MealsEnPlace.Api.Models.Entities.RecipeIngredient"/>.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The original measure string preserved from import
    /// (e.g., "1 can chopped tomatoes", "1 jar marinara sauce").
    /// This is what the user needs to resolve — they must declare the actual
    /// net weight or volume of the named container.
    /// </summary>
    public string Notes { get; init; } = string.Empty;
}
