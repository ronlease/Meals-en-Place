namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Dimensional type of a unit of measure. Cross-type conversions are never attempted
/// without ingredient-specific density data.
/// </summary>
public enum UomType
{
    /// <summary>
    /// No fixed conversion exists. Requires user declaration before participating
    /// in matching math.
    /// </summary>
    Arbitrary,

    /// <summary>Discrete items. Base unit: each (ea).</summary>
    Count,

    /// <summary>Fluid volume. Base unit: milliliter (ml).</summary>
    Volume,

    /// <summary>Mass. Base unit: gram (g).</summary>
    Weight
}
