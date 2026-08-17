namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Request body for the Todoist Test Connection endpoint. When <see cref="Token"/>
/// is provided, that candidate token is used for the test call without modifying
/// the persisted token. When omitted, the currently resolved token (encrypted
/// store first, user-secret fallback) is used.
/// </summary>
public sealed class TestTodoistTokenRequest
{
    /// <summary>Candidate token to test. Null signals "use the currently configured token."</summary>
    public string? Token { get; init; }
}
