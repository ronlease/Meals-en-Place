namespace MealsEnPlace.Api.Infrastructure.Claude;

/// <summary>
/// Minimal Anthropic API client used exclusively by the Test Connection endpoint
/// to verify a candidate API key against the real service. Every other Claude
/// integration path currently runs through the stubbed <see cref="ClaudeService"/>;
/// this client exists so users can prove a pasted key works before committing it.
/// </summary>
public interface IAnthropicTestClient
{
    /// <summary>
    /// Issues the cheapest possible Messages API call using <paramref name="token"/>
    /// and reports success or the Anthropic-reported error message.
    /// </summary>
    Task<AnthropicTestResult> PingAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IAnthropicTestClient.PingAsync"/>.</summary>
public sealed class AnthropicTestResult
{
    /// <summary>Error message reported by Anthropic on failure. Null on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when the call completed with a 2xx status.</summary>
    public bool Success { get; init; }
}
