namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Request body for the Test Connection endpoint. When <see cref="Token"/> is
/// provided, that candidate key is used for the test call without modifying the
/// persisted key. When omitted, the currently persisted key is used.
/// </summary>
public sealed class TestClaudeTokenRequest
{
    /// <summary>Candidate token to test. Null signals "use the persisted token."</summary>
    public string? Token { get; init; }
}
