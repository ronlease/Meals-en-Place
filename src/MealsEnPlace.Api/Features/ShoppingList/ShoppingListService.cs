using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Compares meal plan ingredient requirements against current inventory to produce
/// a shopping list of deficit items.
/// </summary>
public class ShoppingListService(
    MealsEnPlaceDbContext dbContext,
    IUomConversionService uomConversionService,
    UomDisplayConverter uomDisplayConverter) : IShoppingListService
{
    /// <inheritdoc />
    public async Task<List<ShoppingListItemResponse>> GenerateShoppingListAsync(
        Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.MealPlans
            .Include(mp => mp.Slots).ThenInclude(s => s.Recipe).ThenInclude(r => r.RecipeIngredients).ThenInclude(ri => ri.Uom)
            .Include(mp => mp.Slots).ThenInclude(s => s.Recipe).ThenInclude(r => r.RecipeIngredients).ThenInclude(ri => ri.CanonicalIngredient)
            .FirstOrDefaultAsync(mp => mp.Id == mealPlanId, cancellationToken);

        if (plan is null) return [];

        // Aggregate required quantities per ingredient in base units
        var required = new Dictionary<Guid, RequiredIngredient>();

        foreach (var slot in plan.Slots)
        {
            foreach (var ri in slot.Recipe.RecipeIngredients)
            {
                if (ri.UomId is null || ri.Uom is null) continue;

                var conv = await uomConversionService.ConvertToBaseUnitsAsync(ri.Quantity, ri.UomId.Value, cancellationToken);
                if (!conv.Success) continue;

                if (!required.TryGetValue(ri.CanonicalIngredientId, out var entry))
                {
                    entry = new RequiredIngredient
                    {
                        BaseQuantity = 0m,
                        CanonicalIngredient = ri.CanonicalIngredient,
                        UomType = ri.Uom.UomType
                    };
                    required[ri.CanonicalIngredientId] = entry;
                }
                entry.BaseQuantity += conv.ConvertedQuantity;
            }
        }

        // Load current inventory and aggregate available per ingredient in base units
        var inventory = await dbContext.InventoryItems
            .Include(i => i.Uom)
            .ToListAsync(cancellationToken);

        var available = new Dictionary<Guid, decimal>();
        foreach (var item in inventory)
        {
            var conv = await uomConversionService.ConvertToBaseUnitsAsync(item.Quantity, item.UomId, cancellationToken);
            if (!conv.Success) continue;

            if (!available.TryGetValue(item.CanonicalIngredientId, out var total))
                total = 0m;
            available[item.CanonicalIngredientId] = total + conv.ConvertedQuantity;
        }

        // Compute deficit and persist
        // First, remove any previous list for this plan
        var existing = await dbContext.ShoppingListItems
            .Where(sli => sli.MealPlanId == mealPlanId)
            .ToListAsync(cancellationToken);
        dbContext.ShoppingListItems.RemoveRange(existing);

        // Determine base UOM IDs
        var baseUoms = await dbContext.UnitsOfMeasure
            .Where(u => u.BaseUomId == null)
            .ToDictionaryAsync(u => u.UomType, cancellationToken);

        var newItems = new List<ShoppingListItem>();
        foreach (var (ingredientId, req) in required)
        {
            var avail = available.GetValueOrDefault(ingredientId, 0m);
            var deficit = req.BaseQuantity - avail;
            if (deficit <= 0m) continue;

            if (!baseUoms.TryGetValue(req.UomType, out var baseUom)) continue;

            newItems.Add(new ShoppingListItem
            {
                CanonicalIngredientId = ingredientId,
                Id = Guid.NewGuid(),
                MealPlanId = mealPlanId,
                Quantity = deficit,
                UomId = baseUom.Id
            });
        }

        dbContext.ShoppingListItems.AddRange(newItems);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetShoppingListAsync(mealPlanId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ShoppingListItemResponse>> GetShoppingListAsync(
        Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ShoppingListItems
            .Include(sli => sli.CanonicalIngredient)
            .Include(sli => sli.Uom)
            .Where(sli => sli.MealPlanId == mealPlanId)
            .OrderBy(sli => sli.CanonicalIngredient.Category)
            .ThenBy(sli => sli.CanonicalIngredient.Name)
            .ToListAsync(cancellationToken);

        var result = new List<ShoppingListItemResponse>();
        foreach (var item in items)
        {
            var (displayQty, displayAbbrev) = await uomDisplayConverter.ConvertAsync(
                item.Quantity, item.Uom.UomType, cancellationToken);

            result.Add(new ShoppingListItemResponse
            {
                CanonicalIngredientName = item.CanonicalIngredient.Name,
                Category = item.CanonicalIngredient.Category,
                Id = item.Id,
                Notes = item.Notes,
                Quantity = displayQty,
                UomAbbreviation = displayAbbrev
            });
        }
        return result;
    }

    private sealed class RequiredIngredient
    {
        public decimal BaseQuantity { get; set; }
        public required CanonicalIngredient CanonicalIngredient { get; init; }
        public required UomType UomType { get; init; }
    }
}
