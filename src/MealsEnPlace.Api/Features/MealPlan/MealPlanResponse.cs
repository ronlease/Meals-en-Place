using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Response DTO for a meal plan.
/// </summary>
public record MealPlanResponse
{
    /// <summary>When this plan was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Meal plan ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Individual day/slot assignments.</summary>
    public required List<MealPlanSlotResponse> Slots { get; init; }

    /// <summary>The Monday that begins the plan week.</summary>
    public required DateOnly WeekStartDate { get; init; }
}

/// <summary>
/// Response DTO for a single meal plan slot.
/// </summary>
public record MealPlanSlotResponse
{
    /// <summary>
    /// UTC timestamp when the user marked this slot as eaten. Null when the
    /// slot is still pending (MEP-027).
    /// </summary>
    public DateTime? ConsumedAt { get; init; }

    /// <summary>Cuisine type of the assigned recipe.</summary>
    public required string CuisineType { get; init; }

    /// <summary>Day of the week.</summary>
    public required DayOfWeek DayOfWeek { get; init; }

    /// <summary>Slot ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Meal occasion.</summary>
    public required MealSlot MealSlot { get; init; }

    /// <summary>Assigned recipe ID.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>Assigned recipe title.</summary>
    public required string RecipeTitle { get; init; }
}
