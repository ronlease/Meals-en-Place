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
    /// Returns a single page of local recipes ordered by title, with pagination metadata.
    /// </summary>
    /// <param name="page">1-based page number. Values below 1 are clamped to 1.</param>
    /// <param name="pageSize">Items per page. Clamped to [1, <see cref="RecipeImportService.MaxPageSize"/>].</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<RecipeListItemDto>> GetPagedLocalRecipesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the full detail of a single local recipe by ID, or null if not found.</summary>
    Task<RecipeDetailDto?> GetRecipeDetailAsync(Guid id, CancellationToken cancellationToken = default);
}
