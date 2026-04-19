using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// An inventory item's quantity converted to metric base units,
/// used by recipe matching and meal plan scoring.
/// </summary>
/// <param name="BaseQuantity">Quantity expressed in the base unit (ml, g, or ea).</param>
/// <param name="UnitOfMeasureType">The dimensional type of the unit.</param>
/// <param name="ExpiryDate">Optional expiry date of the source inventory item.</param>
public sealed record InventoryBaseEntry(decimal BaseQuantity, UnitOfMeasureType UnitOfMeasureType, DateOnly? ExpiryDate);

/// <summary>
/// Shared helpers for loading and converting inventory items to base units.
/// </summary>
public static class InventoryBaseHelper
{
    /// <summary>
    /// Converts a list of inventory items to base-unit entries grouped by canonical ingredient.
    /// </summary>
    public static async Task<Dictionary<Guid, List<InventoryBaseEntry>>> ConvertToBaseUnitsAsync(
        List<InventoryItem> inventory, IUnitOfMeasureConversionService unitOfMeasureConversionService, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<InventoryBaseEntry>>();
        foreach (var item in inventory)
        {
            var conversion = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(item.Quantity, item.UnitOfMeasureId, cancellationToken);
            if (!conversion.Success) continue;

            if (!result.TryGetValue(item.CanonicalIngredientId, out var entries))
            {
                entries = [];
                result[item.CanonicalIngredientId] = entries;
            }
            entries.Add(new InventoryBaseEntry(conversion.ConvertedQuantity, item.UnitOfMeasure!.UnitOfMeasureType, item.ExpiryDate));
        }
        return result;
    }

    /// <summary>
    /// Loads all inventory items with their CanonicalIngredient and UnitOfMeasure navigations.
    /// </summary>
    public static Task<List<InventoryItem>> LoadInventoryAsync(
        MealsEnPlaceDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.InventoryItems.AsNoTracking()
            .Include(i => i.CanonicalIngredient)
            .Include(i => i.UnitOfMeasure)
            .ToListAsync(cancellationToken);
    }
}
