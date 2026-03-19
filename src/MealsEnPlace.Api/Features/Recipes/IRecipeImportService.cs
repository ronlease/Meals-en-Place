namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Orchestrates searching TheMealDB and importing recipes into the local library.
/// </summary>
public interface IRecipeImportService
{
    /// <summary>Creates a new recipe manually and persists it to the local library.</summary>
    Task<RecipeDetailDto> CreateRecipeAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns all local recipes with resolution status.</summary>
    Task<IReadOnlyList<RecipeListItemDto>> GetAllLocalRecipesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the full detail of a single local recipe by ID, or null if not found.</summary>
    Task<RecipeDetailDto?> GetRecipeDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Imports a recipe from TheMealDB by meal ID.</summary>
    Task<RecipeImportResultDto> ImportByIdAsync(string mealDbId, CancellationToken cancellationToken = default);

    /// <summary>Searches TheMealDB by name.</summary>
    Task<IReadOnlyList<RecipeSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Searches TheMealDB by category.</summary>
    Task<IReadOnlyList<RecipeSearchResultDto>> SearchByCategoryAsync(string category, CancellationToken cancellationToken = default);
}
