namespace MealsEnPlace.Api.Features.Preferences;

/// <summary>Response DTO for user preferences.</summary>
public sealed class UserPreferencesResponse
{
    /// <summary>The user's preferred display unit system.</summary>
    public string DisplaySystem { get; init; } = string.Empty;
}
