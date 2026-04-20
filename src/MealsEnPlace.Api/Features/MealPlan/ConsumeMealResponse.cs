namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>Response for <c>POST /api/v1/meal-plan-slots/{id}/consume</c>.</summary>
public sealed class ConsumeMealResponse
{
    /// <summary>
    /// True when inventory was deducted (user's <c>AutoDepleteOnConsume</c>
    /// preference was on at consume time). False for state-only consumes.
    /// </summary>
    public bool AutoDepleteApplied { get; init; }

    /// <summary>UTC timestamp recorded as the consume time.</summary>
    public DateTime ConsumedAt { get; init; }

    /// <summary>
    /// Ingredients the recipe called for in larger quantity than inventory
    /// held. The consume still succeeded — inventory was clamped to zero for
    /// each short ingredient. Empty when every ingredient was fully covered.
    /// </summary>
    public IReadOnlyList<ShortIngredientResponse> ShortIngredients { get; init; } = [];
}

/// <summary>One short-ingredient warning surfaced from a consume.</summary>
public sealed class ShortIngredientResponse
{
    /// <summary>Canonical ingredient name.</summary>
    public string IngredientName { get; init; } = string.Empty;

    /// <summary>Amount inventory was short, in the recipe's unit.</summary>
    public decimal ShortBy { get; init; }

    /// <summary>Unit abbreviation for <see cref="ShortBy"/>.</summary>
    public string UnitOfMeasureAbbreviation { get; init; } = string.Empty;
}
