namespace MealsEnPlace.Api.Features.Preferences;

/// <summary>Request DTO for updating user preferences.</summary>
public sealed class UpdateUserPreferencesRequest
{
    /// <summary>
    /// When true, marking a meal plan slot as eaten deducts the recipe's
    /// ingredient quantities from current inventory (MEP-027). Null leaves
    /// the current value unchanged.
    /// </summary>
    public bool? AutoDepleteOnConsume { get; init; }

    /// <summary>The desired display unit system: "Imperial" or "Metric".</summary>
    public string DisplaySystem { get; init; } = string.Empty;
}
