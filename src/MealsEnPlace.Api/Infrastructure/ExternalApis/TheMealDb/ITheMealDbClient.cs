namespace MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;

/// <summary>
/// HTTP client abstraction for the TheMealDB free API.
/// </summary>
public interface ITheMealDbClient
{
    /// <summary>Filters meals by category.</summary>
    Task<IReadOnlyList<TheMealDbMeal>> FilterByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>Fetches a single meal by its TheMealDB ID.</summary>
    Task<TheMealDbMeal?> GetByIdAsync(string mealId, CancellationToken cancellationToken = default);

    /// <summary>Searches meals by name.</summary>
    Task<IReadOnlyList<TheMealDbMeal>> SearchByNameAsync(string query, CancellationToken cancellationToken = default);
}
