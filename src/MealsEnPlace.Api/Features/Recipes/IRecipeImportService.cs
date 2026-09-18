using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Manages the local recipe library: manual creation, lookup, and listing.
/// (Prior to MEP-033 this interface also owned TheMealDB search and import;
/// that surface was removed once the Kaggle ingest became the catalog source
/// under MEP-026.)
/// </summary>
public interface IRecipeImportService
{
    /// <summary>Creates a new recipe manually and persists it to the local library.</summary>
    Task<RecipeDetailDto> CreateRecipeAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single page of local recipes ordered by title, applying any search
    /// and filter predicates specified in <paramref name="query"/>.
    /// </summary>
    /// <param name="query">
    /// Pagination parameters plus optional title search, ingredient search, and
    /// dietary-tag filter. All filter fields are additive (AND semantics).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<RecipeListItemDto>> GetPagedLocalRecipesAsync(
        RecipeSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the full detail of a single local recipe by ID, or null if not found.</summary>
    Task<RecipeDetailDto?> GetRecipeDetailAsync(Guid id, CancellationToken cancellationToken = default);
}
