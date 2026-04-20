using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Computes an expiry-urgency-driven reordering of a meal plan's existing
/// slots (MEP-030). Reassigns <see cref="MealPlanSlot.DayOfWeek"/> within each
/// <see cref="MealSlot"/> (Breakfast recipes shuffle among Breakfast slots,
/// etc.) so recipes that consume expiring ingredients fall on earlier days.
/// Recipes are preserved; only their day assignments move.
/// </summary>
public interface IMealPlanReorderService
{
    /// <summary>
    /// Commits a reordering of the given plan using the same urgency window
    /// the client previewed against. Returns the refreshed meal plan, or null
    /// when the plan does not exist.
    /// </summary>
    Task<MealPlanResponse?> ApplyAsync(
        Guid mealPlanId,
        int urgencyWindowDays,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the proposed reorder without mutating the plan. Null is
    /// returned when the plan does not exist.
    /// </summary>
    Task<ReorderPreviewResponse?> PreviewAsync(
        Guid mealPlanId,
        int urgencyWindowDays,
        CancellationToken cancellationToken = default);
}
