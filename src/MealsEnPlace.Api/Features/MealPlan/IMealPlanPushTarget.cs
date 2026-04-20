namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Pushes a meal plan's slots to an external task provider (MEP-029). Each
/// <see cref="Models.Entities.MealPlanSlot"/> becomes one remote task scheduled
/// for that slot's date. Idempotent on re-push — unchanged slots are no-ops,
/// recipe swaps become PATCH updates, and slots the user has removed from the
/// plan have their remote tasks closed.
/// </summary>
public interface IMealPlanPushTarget
{
    /// <summary>Human-readable provider name, e.g., <c>"Todoist"</c>.</summary>
    string ProviderName { get; }

    /// <summary>Pushes every slot belonging to the meal plan.</summary>
    Task<MealPlanPushResult> PushAsync(
        Guid mealPlanId,
        CancellationToken cancellationToken = default);
}

/// <summary>Counts returned from a meal-plan push.</summary>
public sealed class MealPlanPushResult
{
    /// <summary>Remote tasks closed because the local slot is no longer present.</summary>
    public int Closed { get; init; }

    /// <summary>Remote tasks newly created for slots that hadn't been pushed before.</summary>
    public int Created { get; init; }

    /// <summary>Slots whose content was already reflected remotely.</summary>
    public int Unchanged { get; init; }

    /// <summary>Remote tasks updated because the slot's content changed since the last push.</summary>
    public int Updated { get; init; }
}
