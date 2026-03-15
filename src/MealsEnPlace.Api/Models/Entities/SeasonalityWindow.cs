namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Defines the peak growing season for a produce ingredient in a specific USDA hardiness zone.
/// Kale and Broccoli have two windows each; those are stored as two separate rows.
/// </summary>
public class SeasonalityWindow
{
    /// <summary>The canonical ingredient this window applies to.</summary>
    public Guid CanonicalIngredientId { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The last month of the peak season (inclusive).</summary>
    public Month PeakSeasonEnd { get; set; }

    /// <summary>The first month of the peak season (inclusive).</summary>
    public Month PeakSeasonStart { get; set; }

    /// <summary>USDA hardiness zone this window applies to. Defaults to "7a".</summary>
    public string UsdaZone { get; set; } = "7a";

    // Navigation properties

    /// <summary>The canonical ingredient this window applies to.</summary>
    public CanonicalIngredient CanonicalIngredient { get; set; } = null!;
}
