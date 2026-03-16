using System.Text.Json.Serialization;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;

/// <summary>
/// Top-level wrapper returned by TheMealDB search, lookup, and filter endpoints.
/// The meals array is null when no results are found.
/// </summary>
public sealed class TheMealDbSearchResponse
{
    [JsonPropertyName("meals")]
    public IReadOnlyList<TheMealDbMeal>? Meals { get; init; }
}
