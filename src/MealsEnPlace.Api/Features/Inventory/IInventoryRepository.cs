using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Data-access contract for <see cref="InventoryItem"/> persistence.
/// All methods are async; the repository never applies display conversion.
/// </summary>
public interface IInventoryRepository
{
    /// <summary>Persists a new inventory item and returns the saved entity.</summary>
    Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken cancellationToken = default);

    /// <summary>Removes the inventory item with the given id. No-op if not found.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single inventory item by id with its <see cref="CanonicalIngredient"/>
    /// and <see cref="UnitOfMeasure"/> navigations loaded.
    /// Returns null if no item with that id exists.
    /// </summary>
    Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all inventory items, optionally filtered by storage location,
    /// with <see cref="CanonicalIngredient"/> and <see cref="UnitOfMeasure"/> loaded.
    /// </summary>
    Task<IReadOnlyList<InventoryItem>> ListAsync(
        StorageLocation? location,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the given field updates to an existing inventory item and persists the changes.
    /// Returns the updated entity, or null if no item with that id exists.
    /// </summary>
    Task<InventoryItem?> UpdateAsync(
        Guid id,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken = default);
}
