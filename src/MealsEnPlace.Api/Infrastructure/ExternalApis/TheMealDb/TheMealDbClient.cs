using System.Net.Http.Json;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;

/// <summary>
/// HTTP client for the TheMealDB free API.
/// </summary>
public sealed class TheMealDbClient(IHttpClientFactory httpClientFactory, ILogger<TheMealDbClient> logger)
    : ITheMealDbClient
{
    private const string ClientName = "TheMealDb";

    private static string SanitizeForLogging(string value) =>
        value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TheMealDbMeal>> FilterByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            var response = await client.GetFromJsonAsync<TheMealDbSearchResponse>(
                $"/api/json/v1/1/filter.php?c={Uri.EscapeDataString(category)}", cancellationToken);
            return response?.Meals ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TheMealDB FilterByCategory failed for '{Category}'.", SanitizeForLogging(category));
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<TheMealDbMeal?> GetByIdAsync(string mealId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            var response = await client.GetFromJsonAsync<TheMealDbSearchResponse>(
                $"/api/json/v1/1/lookup.php?i={Uri.EscapeDataString(mealId)}", cancellationToken);
            return response?.Meals?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TheMealDB GetById failed for '{MealId}'.", SanitizeForLogging(mealId));
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TheMealDbMeal>> SearchByNameAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            var response = await client.GetFromJsonAsync<TheMealDbSearchResponse>(
                $"/api/json/v1/1/search.php?s={Uri.EscapeDataString(query)}", cancellationToken);
            return response?.Meals ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TheMealDB SearchByName failed for '{Query}'.", SanitizeForLogging(query));
            return [];
        }
    }
}
