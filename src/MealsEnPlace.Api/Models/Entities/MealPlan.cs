namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A weekly meal plan that assigns recipes to day/slot combinations.
/// </summary>
public class MealPlan
{
    /// <summary>UTC timestamp when this plan was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User-supplied display name for this plan.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The Monday date that begins the week this plan covers.</summary>
    public DateOnly WeekStartDate { get; set; }

    // Navigation properties

    /// <summary>The individual day/slot assignments in this plan.</summary>
    public ICollection<MealPlanSlot> Slots { get; set; } = new List<MealPlanSlot>();
}
