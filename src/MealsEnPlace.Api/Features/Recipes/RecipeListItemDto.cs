using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Summary row for the local recipe library list.
/// <para>
/// <c>IngredientNames</c> was removed (MEP-043): it was never rendered by the recipe
/// browser component and was the sole reason the previous query joined 13.6 M
/// RecipeIngredient rows across the full catalog, causing the 30-second command
/// timeout that made the endpoint return HTTP 500.
/// </para>
/// </summary>
public sealed class RecipeListItemDto
{
    /// <summary>Cuisine type.</summary>
    public string CuisineType { get; init; } = string.Empty;

    /// <summary>Dietary tags classified for this recipe.</summary>
    public IReadOnlyList<DietaryTag> DietaryTags { get; init; } = [];

    /// <summary>Local database ID.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Whether all container references are resolved.
    /// Derived from <c>TotalIngredients &gt; 0 &amp;&amp; UnresolvedCount == 0</c>
    /// as a database-side projection.
    /// </summary>
    public bool IsFullyResolved { get; init; }

    /// <summary>Display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Total ingredient count.</summary>
    public int TotalIngredients { get; init; }

    /// <summary>Number of unresolved container references.</summary>
    public int UnresolvedCount { get; init; }
}
