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
    IUnitOfMeasureConversionService unitOfMeasureConversionService,
    UnitOfMeasureDisplayConverter unitOfMeasureDisplayConverter) : IShoppingListService
{
    /// <inheritdoc />
    public async Task<List<ShoppingListItemResponse>> AddFromRecipeAsync(
        Guid recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await dbContext.Recipes
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.UnitOfMeasure)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.CanonicalIngredient)
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        if (recipe is null) return [];

        // Load current inventory in base units
        var inventory = await dbContext.InventoryItems
            .Include(i => i.UnitOfMeasure)
            .ToListAsync(cancellationToken);

        var available = new Dictionary<Guid, decimal>();
        foreach (var item in inventory)
        {
            var conv = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(item.Quantity, item.UnitOfMeasureId, cancellationToken);
            if (!conv.Success) continue;
            available[item.CanonicalIngredientId] = available.GetValueOrDefault(item.CanonicalIngredientId) + conv.ConvertedQuantity;
        }

        // Load existing standalone items for aggregation
        var existingStandalone = await dbContext.ShoppingListItems
            .Where(sli => sli.MealPlanId == null)
            .ToListAsync(cancellationToken);

        var existingByIngredient = existingStandalone.ToDictionary(sli => sli.CanonicalIngredientId);

        // Determine base unit of measure IDs
        var baseUnitOfMeasures = await dbContext.UnitsOfMeasure
            .Where(u => u.BaseUnitOfMeasureId == null)
            .ToDictionaryAsync(u => u.UnitOfMeasureType, cancellationToken);

        foreach (var ri in recipe.RecipeIngredients)
        {
            if (ri.UnitOfMeasureId is null || ri.UnitOfMeasure is null) continue;

            var conv = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(ri.Quantity, ri.UnitOfMeasureId.Value, cancellationToken);
            if (!conv.Success) continue;
            if (!baseUnitOfMeasures.TryGetValue(ri.UnitOfMeasure.UnitOfMeasureType, out var baseUnitOfMeasure)) continue;

            // Check if we already have enough in inventory
            var avail = available.GetValueOrDefault(ri.CanonicalIngredientId);
            var deficit = conv.ConvertedQuantity - avail;
            if (deficit <= 0m)
            {
                // Reduce available for subsequent ingredients using the same item
                available[ri.CanonicalIngredientId] = avail - conv.ConvertedQuantity;
                continue;
            }

            // Aggregate with existing standalone item
            if (existingByIngredient.TryGetValue(ri.CanonicalIngredientId, out var existing))
            {
                existing.Quantity += conv.ConvertedQuantity;
            }
            else
            {
                var newItem = new ShoppingListItem
                {
                    CanonicalIngredientId = ri.CanonicalIngredientId,
                    Id = Guid.NewGuid(),
                    MealPlanId = null,
                    Quantity = deficit,
                    UnitOfMeasureId = baseUnitOfMeasure.Id
                };
                dbContext.ShoppingListItems.Add(newItem);
                existingByIngredient[ri.CanonicalIngredientId] = newItem;
            }

            // Reduce available so we don't double-count
            available[ri.CanonicalIngredientId] = 0m;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetStandaloneShoppingListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ShoppingListItemResponse>> GenerateShoppingListAsync(
        Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.MealPlans
            .Include(mp => mp.Slots).ThenInclude(s => s.Recipe).ThenInclude(r => r.RecipeIngredients).ThenInclude(ri => ri.UnitOfMeasure)
            .Include(mp => mp.Slots).ThenInclude(s => s.Recipe).ThenInclude(r => r.RecipeIngredients).ThenInclude(ri => ri.CanonicalIngredient)
            .FirstOrDefaultAsync(mp => mp.Id == mealPlanId, cancellationToken);

        if (plan is null) return [];

        // Aggregate required quantities per ingredient in base units
        var required = new Dictionary<Guid, RequiredIngredient>();

        foreach (var slot in plan.Slots)
        {
            foreach (var ri in slot.Recipe.RecipeIngredients)
            {
                if (ri.UnitOfMeasureId is null || ri.UnitOfMeasure is null) continue;

                var conv = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(ri.Quantity, ri.UnitOfMeasureId.Value, cancellationToken);
                if (!conv.Success) continue;

                if (!required.TryGetValue(ri.CanonicalIngredientId, out var entry))
                {
                    entry = new RequiredIngredient
                    {
                        BaseQuantity = 0m,
                        CanonicalIngredient = ri.CanonicalIngredient,
                        UnitOfMeasureType = ri.UnitOfMeasure.UnitOfMeasureType
                    };
                    required[ri.CanonicalIngredientId] = entry;
                }
                entry.BaseQuantity += conv.ConvertedQuantity;
            }
        }

        // Load current inventory and aggregate available per ingredient in base units
        var inventory = await dbContext.InventoryItems
            .Include(i => i.UnitOfMeasure)
            .ToListAsync(cancellationToken);

        var available = new Dictionary<Guid, decimal>();
        foreach (var item in inventory)
        {
            var conv = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(item.Quantity, item.UnitOfMeasureId, cancellationToken);
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

        // Determine base unit of measure IDs
        var baseUnitOfMeasures = await dbContext.UnitsOfMeasure
            .Where(u => u.BaseUnitOfMeasureId == null)
            .ToDictionaryAsync(u => u.UnitOfMeasureType, cancellationToken);

        var newItems = new List<ShoppingListItem>();
        foreach (var (ingredientId, req) in required)
        {
            var avail = available.GetValueOrDefault(ingredientId, 0m);
            var deficit = req.BaseQuantity - avail;
            if (deficit <= 0m) continue;

            if (!baseUnitOfMeasures.TryGetValue(req.UnitOfMeasureType, out var baseUnitOfMeasure)) continue;

            newItems.Add(new ShoppingListItem
            {
                CanonicalIngredientId = ingredientId,
                Id = Guid.NewGuid(),
                MealPlanId = mealPlanId,
                Quantity = deficit,
                UnitOfMeasureId = baseUnitOfMeasure.Id
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
            .Include(sli => sli.UnitOfMeasure)
            .Where(sli => sli.MealPlanId == mealPlanId)
            .OrderBy(sli => sli.CanonicalIngredient.Category)
            .ThenBy(sli => sli.CanonicalIngredient.Name)
            .ToListAsync(cancellationToken);

        return await MapToResponsesAsync(items, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ShoppingListItemResponse>> GetStandaloneShoppingListAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ShoppingListItems
            .Include(sli => sli.CanonicalIngredient)
            .Include(sli => sli.UnitOfMeasure)
            .Where(sli => sli.MealPlanId == null)
            .OrderBy(sli => sli.CanonicalIngredient.Category)
            .ThenBy(sli => sli.CanonicalIngredient.Name)
            .ToListAsync(cancellationToken);

        return await MapToResponsesAsync(items, cancellationToken);
    }

    private async Task<List<ShoppingListItemResponse>> MapToResponsesAsync(
        List<ShoppingListItem> items, CancellationToken cancellationToken)
    {
        var result = new List<ShoppingListItemResponse>();
        foreach (var item in items)
        {
            var (displayQty, displayAbbrev) = await unitOfMeasureDisplayConverter.ConvertAsync(
                item.Quantity, item.UnitOfMeasure.UnitOfMeasureType, cancellationToken);

            result.Add(new ShoppingListItemResponse
            {
                CanonicalIngredientName = item.CanonicalIngredient.Name,
                Category = item.CanonicalIngredient.Category,
                Id = item.Id,
                Notes = item.Notes,
                Quantity = displayQty,
                UnitOfMeasureAbbreviation = displayAbbrev
            });
        }
        return result;
    }

    private sealed class RequiredIngredient
    {
        public decimal BaseQuantity { get; set; }
        public required CanonicalIngredient CanonicalIngredient { get; init; }
        public required UnitOfMeasureType UnitOfMeasureType { get; init; }
    }
}
