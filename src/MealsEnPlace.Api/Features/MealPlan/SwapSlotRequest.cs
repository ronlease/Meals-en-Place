namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Request to swap a meal plan slot to a different recipe.
/// </summary>
public class SwapSlotRequest
{
    /// <summary>The recipe to assign to this slot.</summary>
    public Guid RecipeId { get; set; }
}
