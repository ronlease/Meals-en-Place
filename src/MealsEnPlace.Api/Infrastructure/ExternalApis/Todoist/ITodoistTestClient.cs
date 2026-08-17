namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Minimal Todoist client used exclusively by the Test Connection endpoint to
/// verify a candidate API token against the real service. Distinct from
/// <see cref="ITodoistClient"/> so an invalid candidate token can never
/// accidentally leak into the normal push code path.
/// </summary>
public interface ITodoistTestClient
{
    /// <summary>
    /// Issues <c>GET /rest/v2/projects</c> with <paramref name="token"/> and
    /// reports success or the Todoist-reported error message.
    /// </summary>
    Task<TodoistTestResult> PingAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="ITodoistTestClient.PingAsync"/>.</summary>
public sealed class TodoistTestResult
{
    /// <summary>Error message reported by Todoist on failure. Null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when the call completed with a 2xx status.</summary>
    public bool Success { get; init; }
}
