namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Persists the user's preferred <see cref="ClaudeModel"/>. Unlike <see cref="IClaudeTokenStore"/>,
/// the model choice is not a secret and may be returned directly in API responses.
/// </summary>
public interface IClaudeModelStore
{
    /// <summary>
    /// Returns the persisted model preference, or <see cref="ClaudeModelCatalog.Default"/>
    /// when no preference has been saved or the persisted value is no longer recognized.
    /// </summary>
    Task<ClaudeModel> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists <paramref name="model"/> as the current Claude model preference.</summary>
    Task WriteAsync(ClaudeModel model, CancellationToken cancellationToken = default);
}
