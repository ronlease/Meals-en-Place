namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Persists the user's Anthropic API key to an encrypted file outside the repo,
/// using ASP.NET DataProtection. The raw key is never returned from any API
/// response — only the caller that explicitly reads the token (the Claude HTTP
/// client and the Test Connection endpoint) ever sees the plaintext.
/// </summary>
public interface IClaudeTokenStore
{
    /// <summary>Deletes any persisted token. Safe to call when no token exists.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns true when a token is persisted.</summary>
    Task<bool> HasTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the decrypted token, or null when no token is persisted or decryption fails.
    /// Callers must never surface the returned value in any log or response body.
    /// </summary>
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists <paramref name="token"/> as the current Anthropic API key.</summary>
    Task WriteAsync(string token, CancellationToken cancellationToken = default);
}
