namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Response shape for the Claude token and model endpoints. Only a boolean
/// "configured" flag is returned for the key — the raw key is never surfaced. The
/// selected model is not a secret and is returned as its <see cref="ClaudeModel"/>
/// member name (e.g., <c>"Sonnet5"</c>).
/// </summary>
public sealed class ClaudeTokenStatusResponse
{
    /// <summary>True when a decrypted token is available.</summary>
    public bool Configured { get; init; }

    /// <summary>The currently selected <see cref="ClaudeModel"/>, by member name.</summary>
    public required string Model { get; init; }
}
