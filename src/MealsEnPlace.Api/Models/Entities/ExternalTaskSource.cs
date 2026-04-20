namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Discriminator for the kind of local entity an <see cref="ExternalTaskLink"/>
/// is tracking. Stored as a string in the database for readability and forward
/// compatibility.
/// </summary>
public enum ExternalTaskSource
{
    /// <summary>The link tracks a <see cref="Models.Entities.ShoppingListItem"/>.</summary>
    ShoppingListItem,

    /// <summary>The link tracks a <see cref="Models.Entities.MealPlanSlot"/>.</summary>
    MealPlanSlot
}
