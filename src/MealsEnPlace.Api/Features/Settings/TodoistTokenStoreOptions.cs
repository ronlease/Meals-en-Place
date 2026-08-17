namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// Paths used by <see cref="TodoistTokenStore"/> for the encrypted token file.
/// Populated in <c>Program.cs</c> from <see cref="Environment.SpecialFolder.LocalApplicationData"/>
/// so the location is stable across runs without polluting the repo. The
/// DataProtection key ring is shared with the Claude token store and configured
/// once at application startup.
/// </summary>
public sealed class TodoistTokenStoreOptions
{
    /// <summary>Absolute path to the encrypted token file.</summary>
    public required string TokenFilePath { get; init; }
}
