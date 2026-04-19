namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Paths used by <see cref="ClaudeTokenStore"/> for the encrypted token file
/// and DataProtection key ring. Populated in <c>Program.cs</c> from
/// <see cref="Environment.SpecialFolder.LocalApplicationData"/> so the locations
/// are stable across runs without polluting the repo.
/// </summary>
public sealed class ClaudeTokenStoreOptions
{
    /// <summary>Absolute path to the directory holding the DataProtection key ring.</summary>
    public required string KeyRingDirectory { get; init; }

    /// <summary>Absolute path to the encrypted token file.</summary>
    public required string TokenFilePath { get; init; }
}
