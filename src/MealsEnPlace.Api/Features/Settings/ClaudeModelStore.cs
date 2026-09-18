namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// File-backed <see cref="ClaudeModel"/> preference store. Unlike <see cref="ClaudeTokenStore"/>,
/// the model choice is not a secret, so it is persisted as plain text rather than
/// DataProtection-encrypted ciphertext. The containing folder is created lazily on
/// first write and is never committed to source control.
/// </summary>
public sealed class ClaudeModelStore(ClaudeModelStoreOptions options) : IClaudeModelStore
{
    private readonly string modelFilePath = options.ModelFilePath;

    public async Task<ClaudeModel> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(modelFilePath))
        {
            return ClaudeModelCatalog.Default;
        }

        var raw = await File.ReadAllTextAsync(modelFilePath, cancellationToken);
        return ClaudeModelCatalog.TryParse(raw, out var model) ? model : ClaudeModelCatalog.Default;
    }

    public async Task WriteAsync(ClaudeModel model, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(modelFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(modelFilePath, model.ToString(), cancellationToken);
    }
}
