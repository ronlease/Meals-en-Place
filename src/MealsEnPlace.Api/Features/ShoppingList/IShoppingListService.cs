namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Derives and manages shopping lists from meal plans.
/// </summary>
public interface IShoppingListService
{
    /// <summary>
    /// Generates (or regenerates) a shopping list for the given meal plan by comparing
    /// required ingredients against current inventory.
    /// </summary>
    Task<List<ShoppingListItemResponse>> GenerateShoppingListAsync(Guid mealPlanId, CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted shopping list for the given meal plan.</summary>
    Task<List<ShoppingListItemResponse>> GetShoppingListAsync(Guid mealPlanId, CancellationToken cancellationToken = default);
}
