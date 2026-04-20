namespace MealsEnPlace.Api.Features.Preferences;

/// <summary>Response DTO for user preferences.</summary>
public sealed class UserPreferencesResponse
{
    /// <summary>
    /// When true, marking a meal plan slot as eaten deducts the recipe's
    /// ingredient quantities from current inventory (MEP-027). Default false.
    /// </summary>
    public bool AutoDepleteOnConsume { get; init; }

    /// <summary>The user's preferred display unit system.</summary>
    public string DisplaySystem { get; init; } = string.Empty;
}
