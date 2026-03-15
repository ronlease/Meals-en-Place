namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Controls how quantities are rendered at the API response layer.
/// All internal computation uses metric base units regardless of this setting.
/// </summary>
public enum DisplaySystem
{
    /// <summary>Display quantities in Imperial units (fl oz, cups, quarts, oz, lb).</summary>
    Imperial,

    /// <summary>Display quantities in metric base units (ml, g, ea).</summary>
    Metric
}
