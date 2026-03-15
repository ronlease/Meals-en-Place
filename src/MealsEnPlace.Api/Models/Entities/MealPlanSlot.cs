namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A single recipe assignment within a <see cref="MealPlan"/>, associating a recipe
/// with a specific day of the week and meal slot.
/// </summary>
public class MealPlanSlot
{
    /// <summary>Day of the week for this assignment.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The meal plan this slot belongs to.</summary>
    public Guid MealPlanId { get; set; }

    /// <summary>The meal occasion (Breakfast, Lunch, Dinner, Snack).</summary>
    public MealSlot MealSlot { get; set; }

    /// <summary>The recipe assigned to this slot.</summary>
    public Guid RecipeId { get; set; }

    // Navigation properties

    /// <summary>The meal plan this slot belongs to.</summary>
    public MealPlan MealPlan { get; set; } = null!;

    /// <summary>The recipe assigned to this slot.</summary>
    public Recipe Recipe { get; set; } = null!;
}
