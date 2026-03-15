using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Request body for adding a new inventory item.
/// When <see cref="DeclaredQuantity"/> and <see cref="DeclaredUomId"/> are supplied
/// alongside a container-reference entry string, the system stores the item directly
/// without issuing a <see cref="ContainerReferenceDetectedResponse"/>.
/// </summary>
public sealed class AddInventoryItemRequest
{
    /// <summary>
    /// Id of the canonical ingredient this item maps to.
    /// </summary>
    public Guid CanonicalIngredientId { get; init; }

    /// <summary>
    /// The net quantity declared by the user after a container reference was detected.
    /// Supply this together with <see cref="DeclaredUomId"/> on the second POST to
    /// bypass container-reference detection for a known container size.
    /// Null on the initial entry.
    /// </summary>
    public decimal? DeclaredQuantity { get; init; }

    /// <summary>
    /// The unit of measure for <see cref="DeclaredQuantity"/>.
    /// Null on the initial entry.
    /// </summary>
    public Guid? DeclaredUomId { get; init; }

    /// <summary>
    /// Optional expiry date for this item.
    /// </summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>
    /// Storage location: Pantry, Fridge, or Freezer.
    /// </summary>
    public StorageLocation Location { get; init; }

    /// <summary>
    /// The raw entry string as typed by the user (e.g., "1 can of diced tomatoes").
    /// Used as the Notes field when a container reference is resolved, and as the
    /// input to the container reference detector.
    /// </summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>
    /// Quantity in the unit specified by <see cref="UomId"/>.
    /// Ignored when <see cref="DeclaredQuantity"/> is provided.
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// Unit of measure for <see cref="Quantity"/>.
    /// Ignored when <see cref="DeclaredUomId"/> is provided.
    /// </summary>
    public Guid UomId { get; init; }
}
