namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// A recipe preview from TheMealDB search results.
/// </summary>
public sealed class RecipeSearchResultDto
{
    /// <summary>Whether this recipe has already been imported locally.</summary>
    public bool AlreadyImported { get; init; }

    /// <summary>Broad category (e.g., "Seafood", "Chicken").</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>TheMealDB meal ID.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Thumbnail image URL.</summary>
    public string? Thumbnail { get; init; }

    /// <summary>Display title.</summary>
    public string Title { get; init; } = string.Empty;
}
