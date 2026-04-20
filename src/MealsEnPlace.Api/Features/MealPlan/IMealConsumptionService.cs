using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Consume / unconsume flow for a <see cref="MealPlanSlot"/> (MEP-027 / MEP-031).
/// When the user's <c>AutoDepleteOnConsume</c> preference is on, consuming a
/// slot deducts each recipe ingredient from inventory oldest-expiry-first and
/// writes per-decrement audit rows so an unconsume can restore the same rows.
/// </summary>
public interface IMealConsumptionService
{
    /// <summary>
    /// Marks the slot as eaten, captures the current
    /// <c>AutoDepleteOnConsume</c> preference, deducts inventory when the
    /// preference is on, and returns any short-ingredient warnings.
    /// </summary>
    /// <returns>The resulting consume payload, or null when the slot was not found.</returns>
    Task<ConsumeMealResult?> ConsumeAsync(Guid slotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the slot's consumed state. When the slot was consumed with
    /// auto-deplete on, replays the audit rows to restore inventory to the
    /// same <see cref="InventoryItem"/> rows where possible, or creates new
    /// rows preserving the original location and expiry otherwise.
    /// </summary>
    /// <returns>True when the slot was found and unconsumed; false when not found.</returns>
    Task<bool> UnconsumeAsync(Guid slotId, CancellationToken cancellationToken = default);
}

/// <summary>Result payload returned by <see cref="IMealConsumptionService.ConsumeAsync"/>.</summary>
public sealed class ConsumeMealResult
{
    /// <summary>
    /// True when the consume triggered inventory deduction (the user's
    /// preference was on at consume time). False for state-only consumes.
    /// </summary>
    public bool AutoDepleteApplied { get; init; }

    /// <summary>UTC timestamp recorded as the consume time.</summary>
    public DateTime ConsumedAt { get; init; }

    /// <summary>
    /// One entry per ingredient where the recipe called for more than was
    /// available in inventory. Quantity depleted is clamped to what was on
    /// hand; the shortage is reported here for the UI warning.
    /// </summary>
    public IReadOnlyList<ShortIngredient> ShortIngredients { get; init; } = [];
}

/// <summary>Ingredient that the recipe called for in larger quantity than inventory held.</summary>
public sealed class ShortIngredient
{
    /// <summary>Canonical ingredient name.</summary>
    public string IngredientName { get; init; } = string.Empty;

    /// <summary>Amount by which inventory was short, in the recipe's unit.</summary>
    public decimal ShortBy { get; init; }

    /// <summary>Unit abbreviation for <see cref="ShortBy"/>.</summary>
    public string UnitOfMeasureAbbreviation { get; init; } = string.Empty;
}
