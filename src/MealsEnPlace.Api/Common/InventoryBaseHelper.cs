using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// An inventory item's quantity converted to metric base units,
/// used by recipe matching and meal plan scoring.
/// </summary>
/// <param name="BaseQuantity">Quantity expressed in the base unit (ml, g, or ea).</param>
/// <param name="UomType">The dimensional type of the unit.</param>
/// <param name="ExpiryDate">Optional expiry date of the source inventory item.</param>
public sealed record InventoryBaseEntry(decimal BaseQuantity, UomType UomType, DateOnly? ExpiryDate);

/// <summary>
/// Shared helpers for loading and converting inventory items to base units.
/// </summary>
public static class InventoryBaseHelper
{
    /// <summary>
    /// Converts a list of inventory items to base-unit entries grouped by canonical ingredient.
    /// </summary>
    public static async Task<Dictionary<Guid, List<InventoryBaseEntry>>> ConvertToBaseUnitsAsync(
        List<InventoryItem> inventory, IUomConversionService uomConversionService, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<InventoryBaseEntry>>();
        foreach (var item in inventory)
        {
            var conversion = await uomConversionService.ConvertToBaseUnitsAsync(item.Quantity, item.UomId, cancellationToken);
            if (!conversion.Success) continue;

            if (!result.TryGetValue(item.CanonicalIngredientId, out var entries))
            {
                entries = [];
                result[item.CanonicalIngredientId] = entries;
            }
            entries.Add(new InventoryBaseEntry(conversion.ConvertedQuantity, item.Uom!.UomType, item.ExpiryDate));
        }
        return result;
    }

    /// <summary>
    /// Loads all inventory items with their CanonicalIngredient and Uom navigations.
    /// </summary>
    public static Task<List<InventoryItem>> LoadInventoryAsync(
        MealsEnPlaceDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.InventoryItems.AsNoTracking()
            .Include(i => i.CanonicalIngredient)
            .Include(i => i.Uom)
            .ToListAsync(cancellationToken);
    }
}
