namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A food item the user currently has on hand in their pantry, fridge, or freezer.
/// When a container reference was detected on entry, the original entry string is preserved
/// in <see cref="Notes"/> and the resolved net weight or volume is stored in
/// <see cref="Quantity"/> and <see cref="UomId"/>.
/// </summary>
public class InventoryItem
{
    /// <summary>The canonical ingredient this item maps to.</summary>
    public Guid CanonicalIngredientId { get; set; }

    /// <summary>Optional expiry date. Null means no known expiry.</summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Physical storage location.</summary>
    public StorageLocation Location { get; set; }

    /// <summary>
    /// Preserves the original entry string when a container reference was declared
    /// (e.g., "1 can of diced tomatoes"). Null for items entered with an explicit UOM.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Quantity in the unit specified by <see cref="UomId"/>.
    /// Always the resolved net quantity — never a container count.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>The unit of measure for <see cref="Quantity"/>.</summary>
    public Guid UomId { get; set; }

    // Navigation properties

    /// <summary>The canonical ingredient this item maps to.</summary>
    public CanonicalIngredient CanonicalIngredient { get; set; } = null!;

    /// <summary>The unit of measure for this item's quantity.</summary>
    public UnitOfMeasure Uom { get; set; } = null!;

    /// <summary>Waste alerts generated for this item.</summary>
    public ICollection<WasteAlert> WasteAlerts { get; set; } = new List<WasteAlert>();
}
