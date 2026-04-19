namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Exposes whether a Claude API token is configured for the current request.
/// Services that invoke Claude-backed paths must consult <see cref="IsConfiguredAsync"/>
/// before issuing a call and take their deterministic-only branch when it
/// returns false.
/// </summary>
public interface IClaudeAvailability
{
    /// <summary>
    /// Returns true when a token is persisted and readable. A false result means
    /// every Claude-backed enhancement must be skipped — the deterministic path
    /// is the product of record whenever this returns false.
    /// </summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);
}
