namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Summary of a recipe that has one or more unresolved container references.
/// Used to populate the resolution queue UI so the user can see which recipes
/// are awaiting declaration before they enter the matching pool.
/// </summary>
public sealed class UnresolvedRecipeResponse
{
    /// <summary>Primary key of the <see cref="MealsEnPlace.Api.Models.Entities.Recipe"/>.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Display title of the recipe (e.g., "Chili Con Carne").
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The ingredient lines that still have an unresolved container reference,
    /// ordered alphabetically by canonical ingredient name.
    /// </summary>
    public IReadOnlyList<UnresolvedIngredientResponse> UnresolvedIngredients { get; init; }
        = [];
}
