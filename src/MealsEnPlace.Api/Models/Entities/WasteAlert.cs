namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A system-generated notice that an inventory item is approaching expiry and matches
/// one or more available fully-resolved recipes. Dismissed alerts are soft-deleted
/// via <see cref="DismissedAt"/>.
/// </summary>
public class WasteAlert
{
    /// <summary>UTC timestamp when this alert was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the user dismissed this alert. Null if still active.</summary>
    public DateTime? DismissedAt { get; set; }

    /// <summary>The expiry date of the inventory item that triggered this alert.</summary>
    public DateOnly ExpiryDate { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The inventory item that is approaching expiry.</summary>
    public Guid InventoryItemId { get; set; }

    /// <summary>
    /// Ids of fully-resolved recipes that can use the expiring item.
    /// Stored as a PostgreSQL JSON column.
    /// </summary>
    public List<Guid> MatchedRecipeIds { get; set; } = new List<Guid>();

    // Navigation properties

    /// <summary>The inventory item that triggered this alert.</summary>
    public InventoryItem InventoryItem { get; set; } = null!;
}
