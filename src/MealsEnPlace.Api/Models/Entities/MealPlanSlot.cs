namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// A single recipe assignment within a <see cref="MealPlan"/>, associating a recipe
/// with a specific day of the week and meal slot.
/// </summary>
public class MealPlanSlot
{
    /// <summary>
    /// Timestamp (UTC) when the user marked this slot as eaten. Null when the
    /// slot has never been consumed (or was unconsumed). See MEP-027.
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>
    /// Captures the user's <c>AutoDepleteOnConsume</c> preference at the moment
    /// the slot was consumed. Null when the slot has never been consumed. True
    /// means inventory was deducted and the matching <see cref="ConsumeAuditEntry"/>
    /// rows exist to drive the MEP-031 restore flow. False means the consume
    /// was state-only; unconsume must leave inventory untouched.
    /// </summary>
    public bool? ConsumedWithAutoDeplete { get; set; }

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

    /// <summary>
    /// Per-decrement audit rows written when the slot was consumed with
    /// auto-deplete on. Drives the MEP-031 restore flow.
    /// </summary>
    public ICollection<ConsumeAuditEntry> ConsumeAuditEntries { get; set; } = new List<ConsumeAuditEntry>();

    /// <summary>The meal plan this slot belongs to.</summary>
    public MealPlan MealPlan { get; set; } = null!;

    /// <summary>The recipe assigned to this slot.</summary>
    public Recipe Recipe { get; set; } = null!;
}
