namespace MealsEnPlace.Api.Infrastructure.Claude;

/// <summary>
/// Stub implementation of <see cref="IClaudeService"/>.
/// <para>
/// TODO: Replace with a real Anthropic API client once the HTTP client and prompt
/// management infrastructure is wired up. The real implementation must:
/// <list type="bullet">
///   <item><description>Read the Anthropic API key from <c>IConfiguration</c> (dotnet user-secrets key: <c>Claude:ApiKey</c>).</description></item>
///   <item><description>POST a structured JSON prompt to the Anthropic Messages API.</description></item>
///   <item><description>Deserialize the JSON response into the appropriate result type.</description></item>
///   <item><description>Handle API errors (rate limits, network failures, malformed responses) by returning a degraded result with <see cref="ClaudeConfidence.Low"/> — never throw.</description></item>
/// </list>
/// </para>
/// </summary>
public class ClaudeService : IClaudeService
{
    /// <inheritdoc />
    /// <remarks>
    /// Stub: returns a <see cref="ClaudeConfidence.Low"/> result with a placeholder message.
    /// The caller (<see cref="MealsEnPlace.Api.Common.UomNormalizationService"/>) will surface
    /// this to the user rather than applying it silently.
    /// </remarks>
    public Task<UomResolutionResult> ResolveUomAsync(string colloquialQuantity, string ingredientName)
    {
        var result = new UomResolutionResult
        {
            Confidence = ClaudeConfidence.Low,
            Notes = "Claude integration not yet configured. Please declare the quantity and unit manually.",
            ResolvedQuantity = 0m,
            ResolvedUom = string.Empty
        };

        return Task.FromResult(result);
    }
}
