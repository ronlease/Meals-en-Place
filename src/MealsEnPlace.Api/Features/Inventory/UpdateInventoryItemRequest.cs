using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Request body for updating an existing inventory item.
/// All fields are required — supply the full current state of the item with the desired changes.
/// </summary>
public sealed class UpdateInventoryItemRequest
{
    /// <summary>Optional expiry date. Pass null to clear an existing expiry.</summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>Storage location: Pantry, Fridge, or Freezer.</summary>
    public StorageLocation Location { get; init; }

    /// <summary>Optional notes field. Pass null to clear existing notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Updated quantity in the unit specified by <see cref="UnitOfMeasureId"/>.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Unit of measure for <see cref="Quantity"/>.</summary>
    public Guid UnitOfMeasureId { get; init; }
}
