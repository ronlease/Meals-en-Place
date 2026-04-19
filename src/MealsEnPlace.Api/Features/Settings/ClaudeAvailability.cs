namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Default <see cref="IClaudeAvailability"/> — a token is available when the
/// <see cref="IClaudeTokenStore"/> can return a non-empty decrypted value.
/// </summary>
public sealed class ClaudeAvailability(IClaudeTokenStore tokenStore) : IClaudeAvailability
{
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var token = await tokenStore.ReadAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(token);
    }
}
