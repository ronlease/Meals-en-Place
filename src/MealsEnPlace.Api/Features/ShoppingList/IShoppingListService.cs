namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Derives and manages shopping lists from meal plans and ad-hoc recipe additions.
/// </summary>
public interface IShoppingListService
{
    /// <summary>
    /// Adds a recipe's ingredients to the standalone shopping list, aggregating with existing items.
    /// Compares against current inventory and only adds deficit quantities.
    /// </summary>
    Task<List<ShoppingListItemResponse>> AddFromRecipeAsync(Guid recipeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates (or regenerates) a shopping list for the given meal plan by comparing
    /// required ingredients against current inventory.
    /// </summary>
    Task<List<ShoppingListItemResponse>> GenerateShoppingListAsync(Guid mealPlanId, CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted shopping list for the given meal plan.</summary>
    Task<List<ShoppingListItemResponse>> GetShoppingListAsync(Guid mealPlanId, CancellationToken cancellationToken = default);

    /// <summary>Returns the standalone shopping list (items not tied to a meal plan).</summary>
    Task<List<ShoppingListItemResponse>> GetStandaloneShoppingListAsync(CancellationToken cancellationToken = default);
}
