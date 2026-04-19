using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Discriminated result from <see cref="IContainerResolutionService.ResolveAsync"/>.
/// Exactly one of the outcome properties will be set.
/// </summary>
public sealed class ContainerResolutionResult
{
    /// <summary>
    /// Human-readable validation error message when the request was rejected
    /// (e.g., quantity not positive, unit of measure not found).
    /// Populated only when <see cref="IsValidationError"/> is true.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// True when the ingredient was not found or the recipe does not own it.
    /// </summary>
    public bool IsIngredientNotFound { get; init; }

    /// <summary>True when the recipe does not exist.</summary>
    public bool IsRecipeNotFound { get; init; }

    /// <summary>True when the request failed a validation rule.</summary>
    public bool IsValidationError { get; init; }

    /// <summary>The updated ingredient on success. Null on any failure outcome.</summary>
    public RecipeIngredient? ResolvedIngredient { get; init; }

    /// <summary>True when resolution succeeded and <see cref="ResolvedIngredient"/> is set.</summary>
    public bool IsSuccess => ResolvedIngredient is not null;

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>Creates a not-found result for the ingredient.</summary>
    public static ContainerResolutionResult IngredientNotFound() =>
        new() { IsIngredientNotFound = true };

    /// <summary>Creates a not-found result for the recipe.</summary>
    public static ContainerResolutionResult RecipeNotFound() =>
        new() { IsRecipeNotFound = true };

    /// <summary>Creates a success result wrapping the resolved ingredient.</summary>
    public static ContainerResolutionResult Success(RecipeIngredient ingredient) =>
        new() { ResolvedIngredient = ingredient };

    /// <summary>Creates a validation-error result with the supplied message.</summary>
    public static ContainerResolutionResult ValidationError(string message) =>
        new() { ErrorMessage = message, IsValidationError = true };
}
