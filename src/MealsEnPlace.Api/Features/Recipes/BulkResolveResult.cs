namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Outcome of <see cref="IContainerResolutionService.BulkResolveAsync"/>.
/// Either a success (with the count of rows updated) or a validation error
/// (with a message suitable for surfacing to the user).
/// </summary>
public sealed class BulkResolveResult
{
    /// <summary>
    /// Number of <c>RecipeIngredient</c> rows updated by the bulk resolve.
    /// Zero when the request was valid but no matching unresolved rows existed.
    /// </summary>
    public int AffectedCount { get; init; }

    /// <summary>Validation error message when <see cref="IsValidationError"/> is true.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True when the request failed validation.</summary>
    public bool IsValidationError { get; init; }

    public static BulkResolveResult Success(int affectedCount) =>
        new() { AffectedCount = affectedCount };

    public static BulkResolveResult ValidationError(string message) =>
        new() { ErrorMessage = message, IsValidationError = true };
}
