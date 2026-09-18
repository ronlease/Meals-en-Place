namespace MealsEnPlace.Api.Features.Settings;

/// <summary>Request body for <c>POST /api/v1/settings/claude/model</c>.</summary>
public sealed class SaveClaudeModelRequest
{
    /// <summary>The <see cref="ClaudeModel"/> member name to select (e.g., <c>"Sonnet5"</c>).</summary>
    public string? Model { get; init; }
}
