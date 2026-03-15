namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A single ingredient line in a recipe, joining a <see cref="Recipe"/> to a
/// <see cref="CanonicalIngredient"/> with quantity and unit of measure.
/// When a container reference was detected on import, <see cref="IsContainerResolved"/>
/// is false and the original measure string is preserved in <see cref="Notes"/>.
/// </summary>
public class RecipeIngredient
{
    /// <summary>The canonical ingredient used in this line.</summary>
    public Guid CanonicalIngredientId { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// True when no container reference was detected, or when the user has declared
    /// the container's net weight or volume. False for unresolved container references.
    /// Only resolved ingredients participate in recipe matching math.
    /// </summary>
    public bool IsContainerResolved { get; set; }

    /// <summary>
    /// Preserves the original recipe measure string when a container reference was
    /// declared (e.g., "1 can chopped tomatoes"). Null when no container reference
    /// was involved.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Resolved quantity in the unit specified by <see cref="UomId"/>.
    /// 0 for unresolved container references.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>The recipe this ingredient belongs to.</summary>
    public Guid RecipeId { get; set; }

    /// <summary>
    /// The unit of measure for <see cref="Quantity"/>. Null for unresolved
    /// container references.
    /// </summary>
    public Guid? UomId { get; set; }

    // Navigation properties

    /// <summary>The canonical ingredient for this line.</summary>
    public CanonicalIngredient CanonicalIngredient { get; set; } = null!;

    /// <summary>The recipe this line belongs to.</summary>
    public Recipe Recipe { get; set; } = null!;

    /// <summary>The unit of measure for this line's quantity.</summary>
    public UnitOfMeasure? Uom { get; set; }
}
