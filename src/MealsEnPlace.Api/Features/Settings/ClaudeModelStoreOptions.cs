namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Path used by <see cref="ClaudeModelStore"/> for the persisted model preference file.
/// Populated in <c>Program.cs</c> from <see cref="Environment.SpecialFolder.LocalApplicationData"/>
/// so the location is stable across runs without polluting the repo.
/// </summary>
public sealed class ClaudeModelStoreOptions
{
    /// <summary>Absolute path to the plain-text file holding the model preference.</summary>
    public required string ModelFilePath { get; init; }
}
