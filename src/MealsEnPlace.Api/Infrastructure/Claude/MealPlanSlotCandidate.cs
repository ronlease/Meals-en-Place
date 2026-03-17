using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Infrastructure.Claude;

/// <summary>
/// A candidate slot assignment for Claude to review during meal plan optimization.
/// </summary>
public sealed class MealPlanSlotCandidate
{
    /// <summary>Day of the week for this slot.</summary>
    public DayOfWeek DayOfWeek { get; init; }

    /// <summary>Meal occasion.</summary>
    public MealSlot MealSlot { get; init; }

    /// <summary>Assigned recipe ID.</summary>
    public Guid RecipeId { get; init; }

    /// <summary>Assigned recipe title.</summary>
    public string RecipeTitle { get; init; } = string.Empty;

    /// <summary>Overall score used for ranking.</summary>
    public decimal Score { get; init; }

    /// <summary>Whether the recipe uses in-season ingredients.</summary>
    public bool SeasonalAffinity { get; init; }

    /// <summary>Waste reduction bonus from expiry-imminent ingredients.</summary>
    public decimal WasteReductionScore { get; init; }
}
