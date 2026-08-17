using MealsEnPlace.Api.Features.Settings;
using Microsoft.Extensions.Options;

namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Two-tier resolver: the DataProtection-encrypted file written by the Settings
/// UI (MEP-035) wins, and the legacy <c>Todoist:Token</c> user secret remains as
/// a fallback for developers who haven't migrated yet. When both are set, the
/// encrypted store takes precedence so a token saved via the UI never silently
/// defers to a stale user-secret value.
/// </summary>
public sealed class TodoistTokenResolver(
    IOptions<TodoistOptions> options,
    ITodoistTokenStore tokenStore) : ITodoistTokenResolver
{
    public async Task<bool> HasTokenAsync(CancellationToken cancellationToken = default)
    {
        return !string.IsNullOrWhiteSpace(await ResolveAsync(cancellationToken));
    }

    public async Task<string?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var stored = await tokenStore.ReadAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        var fallback = options.Value.Token;
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }
}
