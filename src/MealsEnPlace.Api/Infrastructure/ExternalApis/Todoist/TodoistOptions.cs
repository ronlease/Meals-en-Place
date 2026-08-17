namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Binds from <c>IConfiguration</c> under the <c>Todoist</c> section. <see cref="Token"/>
/// is the legacy <c>dotnet user-secrets</c> source — since MEP-035 it is a
/// fallback only; the encrypted DataProtection store wins. <see cref="ProjectId"/>
/// still lives here (MEP-036 will surface associated project IDs and supersede
/// the static user-secret override).
/// </summary>
public sealed class TodoistOptions
{
    /// <summary>Configuration section name — bind via <c>builder.Configuration.GetSection(SectionName)</c>.</summary>
    public const string SectionName = "Todoist";

    /// <summary>
    /// Optional Todoist project ID. When null or empty, pushes target the
    /// Todoist Inbox (the provider's default). MEP-036 will later surface
    /// previously-used project IDs as a quick-pick.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Legacy Todoist personal API token read from <c>dotnet user-secrets</c>.
    /// Consumed only when the DataProtection-encrypted store is empty (MEP-035
    /// fallback). Writing a token via the Settings page takes precedence.
    /// </summary>
    public string? Token { get; set; }
}
