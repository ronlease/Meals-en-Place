namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Response shape for <c>GET /api/v1/settings/claude/status</c> and <c>POST .../token</c>.
/// Only a boolean "configured" flag is returned — the raw key is never surfaced.
/// </summary>
public sealed class ClaudeTokenStatusResponse
{
    /// <summary>True when a decrypted token is available.</summary>
    public bool Configured { get; init; }
}
