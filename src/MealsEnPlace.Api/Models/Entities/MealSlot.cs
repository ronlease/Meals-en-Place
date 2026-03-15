namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// The meal slot within a day to which a recipe is assigned in a meal plan.
/// </summary>
public enum MealSlot
{
    /// <summary>Morning meal.</summary>
    Breakfast,

    /// <summary>Evening meal.</summary>
    Dinner,

    /// <summary>Midday meal.</summary>
    Lunch,

    /// <summary>Between-meal eating occasion.</summary>
    Snack
}
