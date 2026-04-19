using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Response DTO for a single inventory item.
/// Quantity and unit of measure abbreviation are returned as stored — no display conversion is applied.
/// </summary>
public sealed class InventoryItemResponse
{
    /// <summary>Id of the canonical ingredient this item maps to.</summary>
    public Guid CanonicalIngredientId { get; init; }

    /// <summary>Display name of the canonical ingredient.</summary>
    public string CanonicalIngredientName { get; init; } = string.Empty;

    /// <summary>Optional expiry date.</summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>Primary key of the inventory item.</summary>
    public Guid Id { get; init; }

    /// <summary>Storage location.</summary>
    public StorageLocation Location { get; init; }

    /// <summary>
    /// Original entry string preserved when a container reference was resolved.
    /// Null for items entered with an explicit unit of measure.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>Quantity as entered by the user.</summary>
    public decimal Quantity { get; init; }

    /// <summary>unit of measure abbreviation as stored (e.g., "oz", "lb", "ml", "g").</summary>
    public string UnitOfMeasureAbbreviation { get; init; } = string.Empty;

    /// <summary>Id of the unit of measure as stored in the database.</summary>
    public Guid UnitOfMeasureId { get; init; }
}
