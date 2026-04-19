namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Response shape for <c>POST /api/v1/settings/claude/test</c>. Carries the
/// outcome of the live Anthropic ping call plus an optional error message when
/// the call failed.
/// </summary>
public sealed class ClaudeTokenTestResponse
{
    /// <summary>Anthropic-reported error message on failure. Null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when the test call to Anthropic succeeded.</summary>
    public bool Success { get; init; }
}
