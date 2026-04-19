using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Business logic contract for inventory management.
/// Services work exclusively in metric base units; display conversion is applied
/// at the controller layer.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Attempts to add a new inventory item.
    /// <para>
    /// If the entry string in <see cref="AddInventoryItemRequest.Notes"/> contains a
    /// container keyword and no declared size is provided, returns a
    /// <see cref="ContainerReferenceDetectedResponse"/> and does NOT create an item.
    /// </para>
    /// <para>
    /// When <see cref="AddInventoryItemRequest.DeclaredQuantity"/> and
    /// <see cref="AddInventoryItemRequest.DeclaredUnitOfMeasureId"/> are provided, the container
    /// size has already been declared by the user and the item is created directly.
    /// </para>
    /// </summary>
    /// <returns>
    /// Either an <see cref="InventoryItem"/> (success) or a
    /// <see cref="ContainerReferenceDetectedResponse"/> (container reference detected).
    /// </returns>
    Task<object> AddItemAsync(
        AddInventoryItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the inventory item with the given id.</summary>
    Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single inventory item by id, or null if it does not exist.
    /// </summary>
    Task<InventoryItem?> GetItemByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all inventory items, optionally filtered by storage location.
    /// </summary>
    Task<IReadOnlyList<InventoryItem>> ListItemsAsync(
        StorageLocation? location,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing inventory item.
    /// Returns null if no item with the given id exists.
    /// </summary>
    Task<InventoryItem?> UpdateItemAsync(
        Guid id,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken = default);
}
