namespace MealsEnPlace.Api.Features.Settings;

/// <summary>Request body for saving the user's Anthropic API key.</summary>
public sealed class SaveClaudeTokenRequest
{
    /// <summary>
    /// The raw Anthropic API key (e.g., <c>sk-ant-...</c>). Required and must be non-whitespace.
    /// The value is never echoed back in any response and is not written to logs.
    /// </summary>
    public string Token { get; init; } = string.Empty;
}
