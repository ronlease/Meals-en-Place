using Microsoft.AspNetCore.DataProtection;

namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// File-backed Anthropic API key store. The plaintext token is DataProtection-encrypted
/// and written to a single local file under the user's <see cref="Environment.SpecialFolder.LocalApplicationData"/>
/// directory. The containing folder is created lazily on first write and is never
/// committed to source control.
/// </summary>
public sealed class ClaudeTokenStore : IClaudeTokenStore
{
    private const string ProtectorPurpose = "MealsEnPlace.ClaudeToken.v1";

    private readonly IDataProtector protector;
    private readonly string tokenFilePath;

    public ClaudeTokenStore(IDataProtectionProvider dataProtectionProvider, ClaudeTokenStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(options);

        protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        tokenFilePath = options.TokenFilePath;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(tokenFilePath))
        {
            File.Delete(tokenFilePath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(tokenFilePath));
    }

    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(tokenFilePath))
        {
            return null;
        }

        try
        {
            var cipher = await File.ReadAllBytesAsync(tokenFilePath, cancellationToken);
            var plain = protector.Unprotect(cipher);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        catch
        {
            // Corrupted file or key-ring change — treat as if no token is configured.
            return null;
        }
    }

    public async Task WriteAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var directory = Path.GetDirectoryName(tokenFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plain = System.Text.Encoding.UTF8.GetBytes(token);
        var cipher = protector.Protect(plain);
        await File.WriteAllBytesAsync(tokenFilePath, cipher, cancellationToken);
    }
}
