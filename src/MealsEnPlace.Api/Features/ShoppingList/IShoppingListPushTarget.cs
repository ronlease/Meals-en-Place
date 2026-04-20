namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Pushes a shopping list to an external task provider (MEP-028). The
/// <c>PushAsync</c> contract is idempotent: re-pushing the same list should
/// update changed items, close removed items, and create new ones — never
/// duplicate. The concrete provider encapsulates the HTTP shape, while the
/// abstraction keeps the caller (controller + service) provider-agnostic so
/// Google Tasks / Microsoft To Do can slot in later without touching callers.
/// </summary>
public interface IShoppingListPushTarget
{
    /// <summary>Human-readable name of the provider, e.g., <c>"Todoist"</c>.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Pushes the shopping list scoped to <paramref name="mealPlanId"/>, or
    /// the standalone list when null. Returns counts of created / updated /
    /// closed / unchanged tasks so the UI can summarize the outcome.
    /// </summary>
    Task<ShoppingListPushResult> PushAsync(
        Guid? mealPlanId,
        CancellationToken cancellationToken = default);
}

/// <summary>Summary of a push operation's outcome.</summary>
public sealed class ShoppingListPushResult
{
    /// <summary>Tasks that already existed and were closed because their source item was removed.</summary>
    public int Closed { get; init; }

    /// <summary>Tasks newly created in the provider during this push.</summary>
    public int Created { get; init; }

    /// <summary>Tasks where the source item already had a link and the content was unchanged.</summary>
    public int Unchanged { get; init; }

    /// <summary>Tasks that already existed and were updated because the content changed since the last push.</summary>
    public int Updated { get; init; }
}
