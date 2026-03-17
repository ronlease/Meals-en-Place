using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Request to generate a weekly meal plan.
/// </summary>
public class GenerateMealPlanRequest
{
    /// <summary>Optional dietary tag filters. Only recipes matching all tags are considered.</summary>
    public List<DietaryTag>? DietaryTags { get; set; }

    /// <summary>User-supplied name for this plan. Defaults to "Meal Plan - {WeekStartDate}".</summary>
    public string? Name { get; set; }

    /// <summary>If true, prefer recipes using currently in-season ingredients.</summary>
    public bool SeasonalOnly { get; set; }

    /// <summary>
    /// Which meal slots to fill per day. Defaults to Lunch and Dinner for all 7 days.
    /// Keys are days (0 = Sunday through 6 = Saturday), values are the slots to fill.
    /// </summary>
    public Dictionary<DayOfWeek, List<MealSlot>>? SlotPreferences { get; set; }

    /// <summary>The Monday that begins the plan week. Defaults to the current week's Monday.</summary>
    public DateOnly? WeekStartDate { get; set; }
}
