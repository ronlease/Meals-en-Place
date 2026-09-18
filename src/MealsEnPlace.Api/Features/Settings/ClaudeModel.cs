namespace MealsEnPlace.Api.Features.Settings;

/// <summary>
/// The Claude model family available for selection in the AI section of Settings
/// (MEP-052). A single global preference applies to every Claude-backed call site.
/// </summary>
public enum ClaudeModel
{
    /// <summary>Fable 5.1.</summary>
    Fable51,

    /// <summary>Haiku 4.5 — fast and low-cost; suited to high-volume, low-stakes calls.</summary>
    Haiku45,

    /// <summary>Opus 5 — highest quality, highest cost.</summary>
    Opus5,

    /// <summary>Sonnet 5 — balanced quality and cost. The default.</summary>
    Sonnet5
}
