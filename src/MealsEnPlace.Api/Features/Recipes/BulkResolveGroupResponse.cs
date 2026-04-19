namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Response body for the bulk-resolve endpoint. Reports how many
/// <c>RecipeIngredient</c> rows the request updated.
/// </summary>
public sealed class BulkResolveGroupResponse
{
    /// <summary>Number of <c>RecipeIngredient</c> rows updated.</summary>
    public int AffectedCount { get; init; }
}
