namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A canonical unit of measure with an optional conversion factor to its base unit.
/// ConversionFactor converts FROM this unit TO the base unit for its <see cref="UomType"/>.
/// Base units (ml, g, ea) have <see cref="BaseUomId"/> = null and
/// <see cref="ConversionFactor"/> = 1.0.
/// </summary>
public class UnitOfMeasure
{
    /// <summary>Short symbol used in display (e.g., "tsp", "ml", "oz").</summary>
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>
    /// Id of the base unit for this type. Null when this unit is itself the base unit.
    /// </summary>
    public Guid? BaseUomId { get; set; }

    /// <summary>
    /// Factor that converts a quantity in this unit to a quantity in the base unit.
    /// For base units this is 1.0.
    /// </summary>
    public decimal ConversionFactor { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable name (e.g., "Teaspoon", "Milliliter", "Ounce").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Dimensional type of this unit.</summary>
    public UomType UomType { get; set; }

    // Navigation properties

    /// <summary>The base unit that this unit converts into. Null when this is the base.</summary>
    public UnitOfMeasure? BaseUom { get; set; }

    /// <summary>Derived units that convert into this base unit.</summary>
    public ICollection<UnitOfMeasure> DerivedUnits { get; set; } = new List<UnitOfMeasure>();
}
