namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Single source of truth for the Todoist API token at request time. Resolves
/// from the encrypted <c>TodoistTokenStore</c> first, then falls back to the
/// <c>Todoist:Token</c> user secret. Callers must never cache the resolved value
/// across requests — re-read on each use so that a Settings-page update takes
/// effect immediately without an app restart.
/// </summary>
public interface ITodoistTokenResolver
{
    /// <summary>Returns true when any configured source exposes a non-whitespace token.</summary>
    Task<bool> HasTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the effective Todoist token, or null when neither the encrypted
    /// store nor the user-secret fallback has one configured.
    /// </summary>
    Task<string?> ResolveAsync(CancellationToken cancellationToken = default);
}
