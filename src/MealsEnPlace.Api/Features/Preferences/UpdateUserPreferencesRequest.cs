namespace MealsEnPlace.Api.Features.Preferences;

/// <summary>Request DTO for updating user preferences.</summary>
public sealed class UpdateUserPreferencesRequest
{
    /// <summary>The desired display unit system: "Imperial" or "Metric".</summary>
    public string DisplaySystem { get; init; } = string.Empty;
}
