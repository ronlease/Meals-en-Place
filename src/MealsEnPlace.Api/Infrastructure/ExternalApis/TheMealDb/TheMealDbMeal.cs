using System.Text.Json.Serialization;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;

/// <summary>
/// Represents a single meal returned by the TheMealDB API.
/// TheMealDB stores up to 20 ingredient/measure pairs as flat numbered properties.
/// </summary>
public sealed class TheMealDbMeal
{
    [JsonPropertyName("strArea")]
    public string? Area { get; init; }

    [JsonPropertyName("strCategory")]
    public string? Category { get; init; }

    [JsonPropertyName("strInstructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("idMeal")]
    public string? MealId { get; init; }

    [JsonPropertyName("strMeal")]
    public string? MealName { get; init; }

    [JsonPropertyName("strMealThumb")]
    public string? MealThumb { get; init; }

    [JsonPropertyName("strSource")]
    public string? Source { get; init; }

    [JsonPropertyName("strIngredient1")] public string? Ingredient1 { get; init; }
    [JsonPropertyName("strIngredient2")] public string? Ingredient2 { get; init; }
    [JsonPropertyName("strIngredient3")] public string? Ingredient3 { get; init; }
    [JsonPropertyName("strIngredient4")] public string? Ingredient4 { get; init; }
    [JsonPropertyName("strIngredient5")] public string? Ingredient5 { get; init; }
    [JsonPropertyName("strIngredient6")] public string? Ingredient6 { get; init; }
    [JsonPropertyName("strIngredient7")] public string? Ingredient7 { get; init; }
    [JsonPropertyName("strIngredient8")] public string? Ingredient8 { get; init; }
    [JsonPropertyName("strIngredient9")] public string? Ingredient9 { get; init; }
    [JsonPropertyName("strIngredient10")] public string? Ingredient10 { get; init; }
    [JsonPropertyName("strIngredient11")] public string? Ingredient11 { get; init; }
    [JsonPropertyName("strIngredient12")] public string? Ingredient12 { get; init; }
    [JsonPropertyName("strIngredient13")] public string? Ingredient13 { get; init; }
    [JsonPropertyName("strIngredient14")] public string? Ingredient14 { get; init; }
    [JsonPropertyName("strIngredient15")] public string? Ingredient15 { get; init; }
    [JsonPropertyName("strIngredient16")] public string? Ingredient16 { get; init; }
    [JsonPropertyName("strIngredient17")] public string? Ingredient17 { get; init; }
    [JsonPropertyName("strIngredient18")] public string? Ingredient18 { get; init; }
    [JsonPropertyName("strIngredient19")] public string? Ingredient19 { get; init; }
    [JsonPropertyName("strIngredient20")] public string? Ingredient20 { get; init; }

    [JsonPropertyName("strMeasure1")] public string? Measure1 { get; init; }
    [JsonPropertyName("strMeasure2")] public string? Measure2 { get; init; }
    [JsonPropertyName("strMeasure3")] public string? Measure3 { get; init; }
    [JsonPropertyName("strMeasure4")] public string? Measure4 { get; init; }
    [JsonPropertyName("strMeasure5")] public string? Measure5 { get; init; }
    [JsonPropertyName("strMeasure6")] public string? Measure6 { get; init; }
    [JsonPropertyName("strMeasure7")] public string? Measure7 { get; init; }
    [JsonPropertyName("strMeasure8")] public string? Measure8 { get; init; }
    [JsonPropertyName("strMeasure9")] public string? Measure9 { get; init; }
    [JsonPropertyName("strMeasure10")] public string? Measure10 { get; init; }
    [JsonPropertyName("strMeasure11")] public string? Measure11 { get; init; }
    [JsonPropertyName("strMeasure12")] public string? Measure12 { get; init; }
    [JsonPropertyName("strMeasure13")] public string? Measure13 { get; init; }
    [JsonPropertyName("strMeasure14")] public string? Measure14 { get; init; }
    [JsonPropertyName("strMeasure15")] public string? Measure15 { get; init; }
    [JsonPropertyName("strMeasure16")] public string? Measure16 { get; init; }
    [JsonPropertyName("strMeasure17")] public string? Measure17 { get; init; }
    [JsonPropertyName("strMeasure18")] public string? Measure18 { get; init; }
    [JsonPropertyName("strMeasure19")] public string? Measure19 { get; init; }
    [JsonPropertyName("strMeasure20")] public string? Measure20 { get; init; }

    /// <summary>
    /// Returns non-empty ingredient/measure pairs, filtering out blank ingredient slots.
    /// </summary>
    public IReadOnlyList<(string Ingredient, string Measure)> GetIngredientMeasurePairs()
    {
        var ingredients = new[]
        {
            Ingredient1, Ingredient2, Ingredient3, Ingredient4, Ingredient5,
            Ingredient6, Ingredient7, Ingredient8, Ingredient9, Ingredient10,
            Ingredient11, Ingredient12, Ingredient13, Ingredient14, Ingredient15,
            Ingredient16, Ingredient17, Ingredient18, Ingredient19, Ingredient20
        };

        var measures = new[]
        {
            Measure1, Measure2, Measure3, Measure4, Measure5,
            Measure6, Measure7, Measure8, Measure9, Measure10,
            Measure11, Measure12, Measure13, Measure14, Measure15,
            Measure16, Measure17, Measure18, Measure19, Measure20
        };

        return ingredients
            .Zip(measures, (ingredient, measure) => (ingredient, measure))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.ingredient))
            .Select(pair => (pair.ingredient!, pair.measure ?? string.Empty))
            .ToList();
    }
}
