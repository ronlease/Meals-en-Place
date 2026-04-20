namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Binds from <c>IConfiguration</c> under the <c>Todoist</c> section. The token
/// is expected to come from <c>dotnet user-secrets</c> for local development
/// (MEP-028 scope). MEP-035 will later add a Settings-page flow that stores
/// the token in ASP.NET DataProtection at rest alongside the Claude key.
/// </summary>
public sealed class TodoistOptions
{
    /// <summary>Configuration section name — bind via <c>builder.Configuration.GetSection(SectionName)</c>.</summary>
    public const string SectionName = "Todoist";

    /// <summary>True when a non-whitespace token is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);

    /// <summary>
    /// Optional Todoist project ID. When null or empty, pushes target the
    /// Todoist Inbox (the provider's default). MEP-036 will later surface
    /// previously-used project IDs as a quick-pick.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>Todoist personal API token. Empty / null disables the integration.</summary>
    public string? Token { get; set; }
}
