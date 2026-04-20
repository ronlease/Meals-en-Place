using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>Proposed reorder returned by the preview endpoint (MEP-030).</summary>
public sealed class ReorderPreviewResponse
{
    /// <summary>Per-slot before / after day assignments. Empty when no reorder applies.</summary>
    public IReadOnlyList<ReorderedSlotDto> Changes { get; init; } = [];

    /// <summary>
    /// True when at least one slot's day changed. False means the current
    /// order already prioritizes expiring ingredients, or no planned
    /// ingredient is within the urgency window.
    /// </summary>
    public bool HasChanges { get; init; }

    /// <summary>
    /// Human-readable explanation of the no-op result when
    /// <see cref="HasChanges"/> is false. Null when changes were computed.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>Urgency window used for the preview.</summary>
    public int UrgencyWindowDays { get; init; }
}

/// <summary>One slot's before/after day assignment from a reorder preview.</summary>
public sealed class ReorderedSlotDto
{
    /// <summary>Slot identifier; unchanged between preview and apply.</summary>
    public Guid Id { get; init; }

    /// <summary>The meal occasion (Breakfast, Lunch, Dinner, Snack). Unchanged.</summary>
    public MealSlot MealSlot { get; init; }

    /// <summary>Day the slot currently falls on.</summary>
    public DayOfWeek OriginalDay { get; init; }

    /// <summary>Day the slot would move to after the reorder.</summary>
    public DayOfWeek ProposedDay { get; init; }

    /// <summary>Assigned recipe id.</summary>
    public Guid RecipeId { get; init; }

    /// <summary>Assigned recipe title for display.</summary>
    public string RecipeTitle { get; init; } = string.Empty;

    /// <summary>Urgency score contributed by this slot's ingredients.</summary>
    public decimal UrgencyScore { get; init; }
}
