using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.WasteReduction;

/// <summary>
/// Scans inventory for items approaching expiry, matches them against fully-resolved recipes,
/// and creates/manages <see cref="WasteAlert"/> entities.
/// </summary>
public class WasteAlertService(MealsEnPlaceDbContext dbContext) : IWasteAlertService
{
    private const int ExpiryThresholdDays = 3;

    /// <inheritdoc />
    public async Task<bool> DismissAlertAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var alert = await dbContext.WasteAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);

        if (alert is null) return false;

        alert.DismissedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<List<WasteAlertResponse>> EvaluateAlertsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var threshold = today.AddDays(ExpiryThresholdDays);

        // Find inventory items expiring within threshold that have an expiry date set
        var expiringItems = await dbContext.InventoryItems
            .Include(i => i.CanonicalIngredient)
            .Include(i => i.UnitOfMeasure)
            .Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= threshold)
            .ToListAsync(cancellationToken);

        // Load all fully-resolved recipes with their ingredients
        var resolvedRecipes = await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.RecipeIngredients)
            .Where(r => r.RecipeIngredients.Any() && r.RecipeIngredients.All(ri => ri.IsContainerResolved))
            .ToListAsync(cancellationToken);

        // Build a lookup: CanonicalIngredientId → list of recipe IDs that use it
        var ingredientToRecipes = new Dictionary<Guid, List<Guid>>();
        foreach (var recipe in resolvedRecipes)
        {
            foreach (var ri in recipe.RecipeIngredients)
            {
                if (!ingredientToRecipes.TryGetValue(ri.CanonicalIngredientId, out var recipeIds))
                {
                    recipeIds = [];
                    ingredientToRecipes[ri.CanonicalIngredientId] = recipeIds;
                }
                recipeIds.Add(recipe.Id);
            }
        }

        // Load existing active alerts for these inventory items
        var expiringItemIds = expiringItems.Select(i => i.Id).ToHashSet();
        var existingAlerts = await dbContext.WasteAlerts
            .Where(a => expiringItemIds.Contains(a.InventoryItemId) && a.DismissedAt == null)
            .ToListAsync(cancellationToken);
        var existingAlertsByItem = existingAlerts.ToDictionary(a => a.InventoryItemId);

        foreach (var item in expiringItems)
        {
            if (!ingredientToRecipes.TryGetValue(item.CanonicalIngredientId, out var matchedRecipeIds))
                continue;

            var sortedIds = matchedRecipeIds.Distinct().OrderBy(id => id).ToList();

            if (existingAlertsByItem.TryGetValue(item.Id, out var existing))
            {
                // Update if recipe set changed
                var existingSorted = existing.MatchedRecipeIds.OrderBy(id => id).ToList();
                if (!sortedIds.SequenceEqual(existingSorted))
                {
                    existing.MatchedRecipeIds = sortedIds;
                }
            }
            else
            {
                // Create new alert
                dbContext.WasteAlerts.Add(new WasteAlert
                {
                    CreatedAt = DateTime.UtcNow,
                    ExpiryDate = item.ExpiryDate!.Value,
                    Id = Guid.NewGuid(),
                    InventoryItemId = item.Id,
                    MatchedRecipeIds = sortedIds
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetActiveAlertsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<WasteAlertResponse>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var alerts = await dbContext.WasteAlerts
            .Include(a => a.InventoryItem).ThenInclude(i => i.CanonicalIngredient)
            .Include(a => a.InventoryItem).ThenInclude(i => i.UnitOfMeasure)
            .Where(a => a.DismissedAt == null)
            .OrderBy(a => a.ExpiryDate)
            .ToListAsync(cancellationToken);

        // Load matched recipes in one query
        var allRecipeIds = alerts.SelectMany(a => a.MatchedRecipeIds).Distinct().ToList();
        var recipes = await dbContext.Recipes
            .AsNoTracking()
            .Where(r => allRecipeIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return alerts.Select(a => new WasteAlertResponse
        {
            AlertId = a.Id,
            CanonicalIngredientName = a.InventoryItem.CanonicalIngredient.Name,
            CreatedAt = a.CreatedAt,
            DaysUntilExpiry = a.ExpiryDate.DayNumber - today.DayNumber,
            ExpiryDate = a.ExpiryDate,
            InventoryItemId = a.InventoryItemId,
            Location = a.InventoryItem.Location.ToString(),
            MatchedRecipes = a.MatchedRecipeIds
                .Where(id => recipes.ContainsKey(id))
                .Select(id => new WasteAlertRecipeDto
                {
                    CuisineType = recipes[id].CuisineType,
                    RecipeId = id,
                    Title = recipes[id].Title
                })
                .OrderBy(r => r.Title)
                .ToList(),
            Quantity = a.InventoryItem.Quantity,
            UnitOfMeasureAbbreviation = a.InventoryItem.UnitOfMeasure.Abbreviation
        }).ToList();
    }
}
