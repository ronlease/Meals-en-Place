namespace MealsEnPlace.Api.Features.Settings;

/// <summary>Request body for saving the user's Todoist personal API token.</summary>
public sealed class SaveTodoistTokenRequest
{
    /// <summary>
    /// The raw Todoist personal API token. Required and must be non-whitespace.
    /// The value is never echoed back in any response and is not written to logs.
    /// </summary>
    public string Token { get; init; } = string.Empty;
}
