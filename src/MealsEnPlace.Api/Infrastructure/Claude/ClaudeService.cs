using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Infrastructure.Claude;

/// <summary>
/// Stub implementation of <see cref="IClaudeService"/>.
/// <para>
/// TODO: Replace with a real Anthropic API client once the HTTP client and prompt
/// management infrastructure is wired up. The real implementation must:
/// <list type="bullet">
///   <item><description>Read the Anthropic API key from <c>IConfiguration</c> (dotnet user-secrets key: <c>Claude:ApiKey</c>).</description></item>
///   <item><description>POST a structured JSON prompt to the Anthropic Messages API.</description></item>
///   <item><description>Deserialize the JSON response into the appropriate result type.</description></item>
///   <item><description>Handle API errors (rate limits, network failures, malformed responses) by returning a degraded result with <see cref="ClaudeConfidence.Low"/> — never throw.</description></item>
/// </list>
/// </para>
/// </summary>
public class ClaudeService : IClaudeService
{
    /// <inheritdoc />
    /// <remarks>
    /// Stub: returns a <see cref="ClaudeConfidence.Low"/> result with a placeholder message.
    /// The caller (<see cref="MealsEnPlace.Api.Common.UomNormalizationService"/>) will surface
    /// this to the user rather than applying it silently.
    /// </remarks>
    public Task<UomResolutionResult> ResolveUomAsync(string colloquialQuantity, string ingredientName)
    {
        var result = new UomResolutionResult
        {
            Confidence = ClaudeConfidence.Low,
            Notes = "Claude integration not yet configured. Please declare the quantity and unit manually.",
            ResolvedQuantity = 0m,
            ResolvedUom = string.Empty
        };

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Rule-based stub: classifies dietary tags by scanning ingredient names for
    /// known animal products and gluten-containing grains. Will be replaced by
    /// Claude API calls for more nuanced classification.
    /// </remarks>
    public Task<IReadOnlyList<DietaryTag>> ClassifyDietaryTagsAsync(Recipe recipe)
    {
        var ingredientNames = recipe.RecipeIngredients
            .Select(ri => ri.CanonicalIngredient?.Name?.ToLowerInvariant() ?? string.Empty)
            .Where(n => n.Length > 0)
            .ToList();

        if (ingredientNames.Count == 0)
            return Task.FromResult<IReadOnlyList<DietaryTag>>([]);

        var hasMeat = ingredientNames.Any(n =>
            MeatKeywords.Any(k => n.Contains(k)));
        var hasDairy = ingredientNames.Any(n =>
            DairyKeywords.Any(k => n.Contains(k)));
        var hasEggs = ingredientNames.Any(n =>
            EggKeywords.Any(k => n.Contains(k)));
        var hasGluten = ingredientNames.Any(n =>
            GlutenKeywords.Any(k => n.Contains(k)));

        var tags = new List<DietaryTag>();

        // Carnivore: primarily meat/animal-based
        if (hasMeat)
            tags.Add(DietaryTag.Carnivore);

        // Vegetarian: no meat
        if (!hasMeat)
            tags.Add(DietaryTag.Vegetarian);

        // Vegan: no animal products at all
        if (!hasMeat && !hasDairy && !hasEggs)
            tags.Add(DietaryTag.Vegan);

        // DairyFree
        if (!hasDairy)
            tags.Add(DietaryTag.DairyFree);

        // GlutenFree
        if (!hasGluten)
            tags.Add(DietaryTag.GlutenFree);

        return Task.FromResult<IReadOnlyList<DietaryTag>>(tags);
    }

    private static readonly string[] DairyKeywords =
    [
        "butter", "cheese", "cream", "ghee", "milk", "parmesan",
        "ricotta", "yogurt", "yoghurt", "whey", "mozzarella", "cheddar"
    ];

    private static readonly string[] EggKeywords = ["egg"];

    private static readonly string[] GlutenKeywords =
    [
        "barley", "bread", "couscous", "flour", "noodle", "pasta",
        "rye", "semolina", "spaghetti", "wheat"
    ];

    private static readonly string[] MeatKeywords =
    [
        "anchovy", "bacon", "beef", "chicken", "chorizo", "clam", "cod",
        "crab", "duck", "fish", "ham", "lamb", "lobster", "mackerel",
        "mince", "mussel", "oyster", "pork", "prawn", "salami", "salmon",
        "sardine", "sausage", "shrimp", "steak", "tuna", "turkey", "veal"
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Stub: returns candidates unchanged. Real implementation would send the full plan
    /// to Claude for variety and waste-reduction optimization.
    /// </remarks>
    public Task<IReadOnlyList<MealPlanSlotCandidate>> OptimizeMealPlanAsync(
        IReadOnlyList<MealPlanSlotCandidate> candidates, IReadOnlyList<InventoryItem> expiringItems,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(candidates);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SubstitutionSuggestion>> SuggestSubstitutionsAsync(
        Recipe recipe, IReadOnlyList<MissingIngredient> missing, IReadOnlyList<InventoryItem> pantry,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SubstitutionSuggestion>>([]);
    }
}
