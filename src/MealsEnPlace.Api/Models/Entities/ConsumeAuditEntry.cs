namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// One row per <see cref="InventoryItem"/> decrement issued when a
/// <see cref="MealPlanSlot"/> is marked eaten with the user's
/// <c>AutoDepleteOnConsume</c> preference enabled. The set of entries keyed by
/// <see cref="MealPlanSlotId"/> is the restore trail used by MEP-031: when the
/// user unmarks the slot, the service replays the decrement in reverse against
/// the original row when it still exists, or creates a replacement row
/// preserving <see cref="OriginalLocation"/> and <see cref="OriginalExpiryDate"/>.
/// </summary>
public class ConsumeAuditEntry
{
    /// <summary>The canonical ingredient that was depleted.</summary>
    public Guid CanonicalIngredientId { get; set; }

    /// <summary>When this entry was recorded, UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Quantity subtracted from the source inventory row, in the
    /// <see cref="UnitOfMeasure"/> of that row. Restored as-is on unmark.
    /// </summary>
    public decimal DeductedQuantity { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The meal plan slot whose consume produced this entry.</summary>
    public Guid MealPlanSlotId { get; set; }

    /// <summary>
    /// Expiry date of the source row at the time of consume. Copied onto a
    /// freshly-created row if the original row has since been deleted.
    /// </summary>
    public DateOnly? OriginalExpiryDate { get; set; }

    /// <summary>
    /// Storage location of the source row at the time of consume. Copied onto
    /// a freshly-created row if the original row has since been deleted.
    /// </summary>
    public StorageLocation OriginalLocation { get; set; }

    /// <summary>
    /// The <see cref="InventoryItem"/> that was decremented. Not enforced as a
    /// FK because the row may legitimately be deleted between consume and
    /// unconsume; the service handles the null case by creating a replacement
    /// row from <see cref="OriginalLocation"/> and <see cref="OriginalExpiryDate"/>.
    /// </summary>
    public Guid? OriginalInventoryItemId { get; set; }

    /// <summary>Unit of measure for <see cref="DeductedQuantity"/>.</summary>
    public Guid UnitOfMeasureId { get; set; }

    // Navigation properties

    /// <summary>The canonical ingredient that was depleted.</summary>
    public CanonicalIngredient CanonicalIngredient { get; set; } = null!;

    /// <summary>The meal plan slot whose consume produced this entry.</summary>
    public MealPlanSlot MealPlanSlot { get; set; } = null!;

    /// <summary>Unit of measure for <see cref="DeductedQuantity"/>.</summary>
    public UnitOfMeasure UnitOfMeasure { get; set; } = null!;
}
