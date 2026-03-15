using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// EF Core implementation of <see cref="IInventoryRepository"/>.
/// </summary>
public class InventoryRepository(MealsEnPlaceDbContext dbContext) : IInventoryRepository
{
    /// <inheritdoc />
    public async Task<InventoryItem> AddAsync(
        InventoryItem item,
        CancellationToken cancellationToken = default)
    {
        dbContext.InventoryItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.InventoryItems
            .FindAsync([id], cancellationToken);

        if (item is not null)
        {
            dbContext.InventoryItems.Remove(item);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.InventoryItems
            .Include(ii => ii.CanonicalIngredient)
            .Include(ii => ii.Uom)
            .FirstOrDefaultAsync(ii => ii.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InventoryItem>> ListAsync(
        StorageLocation? location,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InventoryItems
            .Include(ii => ii.CanonicalIngredient)
            .Include(ii => ii.Uom)
            .AsQueryable();

        if (location.HasValue)
        {
            query = query.Where(ii => ii.Location == location.Value);
        }

        return await query
            .OrderBy(ii => ii.CanonicalIngredient.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> UpdateAsync(
        Guid id,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.InventoryItems
            .Include(ii => ii.CanonicalIngredient)
            .Include(ii => ii.Uom)
            .FirstOrDefaultAsync(ii => ii.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.ExpiryDate = request.ExpiryDate;
        item.Location   = request.Location;
        item.Notes      = request.Notes;
        item.Quantity   = request.Quantity;
        item.UomId      = request.UomId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }
}
