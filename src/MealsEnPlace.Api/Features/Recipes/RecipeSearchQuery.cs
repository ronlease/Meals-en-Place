using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Encapsulates all query parameters for the paged recipe list endpoint:
/// pagination, title search, ingredient search, and dietary-tag filter.
/// </summary>
/// <param name="DietaryTags">
/// Dietary tags to filter on. When multiple tags are supplied, only recipes that
/// carry <em>all</em> specified tags are returned (AND semantics — the recipe must
/// have every tag in the list). An empty list disables this filter.
/// </param>
/// <param name="IngredientSearch">
/// Optional case-insensitive substring to match against canonical ingredient names.
/// When provided, only recipes that include at least one ingredient whose canonical
/// name contains this term are returned.
/// </param>
/// <param name="Page">1-based page number; values below 1 are clamped to 1.</param>
/// <param name="PageSize">
/// Items per page; clamped to [1, <see cref="RecipeImportService.MaxPageSize"/>].
/// </param>
/// <param name="TitleSearch">
/// Optional case-insensitive substring to match against recipe titles. When
/// provided, only recipes whose title contains this term are returned.
/// </param>
public sealed record RecipeSearchQuery(
    IReadOnlyList<DietaryTag> DietaryTags,
    string? IngredientSearch,
    int Page,
    int PageSize,
    string? TitleSearch);
