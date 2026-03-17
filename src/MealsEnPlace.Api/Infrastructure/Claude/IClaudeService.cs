using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Infrastructure.Claude;

/// <summary>
/// Confidence level for a Claude-resolved UOM or other AI-assisted result.
/// </summary>
public enum ClaudeConfidence
{
    /// <summary>Claude is highly confident in the resolution.</summary>
    High,

    /// <summary>Claude is moderately confident; the result may be surfaced to the user for review.</summary>
    Medium,

    /// <summary>
    /// Claude is not confident, or the unit is Arbitrary.
    /// Results at this level must never be silently applied — surface a prompt to the user.
    /// </summary>
    Low
}

/// <summary>
/// The result of a Claude UOM resolution call for a colloquial or unmappable measure string.
/// </summary>
public sealed class UomResolutionResult
{
    /// <summary>
    /// Confidence level assigned by Claude to the resolution.
    /// Low-confidence results must be surfaced to the user rather than applied silently.
    /// </summary>
    public ClaudeConfidence Confidence { get; init; }

    /// <summary>
    /// Optional notes explaining the resolution or flagging uncertainty
    /// (e.g., "Assumed standard knob size; user may override.").
    /// </summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>
    /// The resolved numeric quantity in the resolved unit.
    /// </summary>
    public decimal ResolvedQuantity { get; init; }

    /// <summary>
    /// The abbreviation of the resolved UOM (e.g., "g", "ml", "ea").
    /// Must match an <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure"/> abbreviation in the database.
    /// </summary>
    public string ResolvedUom { get; init; } = string.Empty;
}

/// <summary>
/// Defines Claude-powered operations used throughout the Meals en Place pipeline.
/// Claude is invoked only when deterministic resolution is not possible.
/// All implementations must handle Claude API errors gracefully and return degraded
/// results rather than throwing.
/// </summary>
public interface IClaudeService
{
    /// <summary>
    /// Resolves a colloquial or unmappable measure string to a canonical quantity and unit.
    /// Invoked only after deterministic UOM lookup fails.
    /// </summary>
    /// <param name="colloquialQuantity">
    /// The raw measure string that could not be resolved deterministically
    /// (e.g., "a knob", "1 head", "a splash").
    /// </param>
    /// <param name="ingredientName">
    /// The ingredient name for context (e.g., "butter", "garlic", "vinegar").
    /// </param>
    /// <returns>
    /// A <see cref="UomResolutionResult"/> with the resolved quantity, unit, and confidence.
    /// If <see cref="UomResolutionResult.Confidence"/> is <see cref="ClaudeConfidence.Low"/>,
    /// the caller must surface a prompt to the user rather than applying the result silently.
    /// </returns>
    /// <summary>
    /// Classifies a recipe's dietary tags by analyzing its ingredients and instructions.
    /// </summary>
    Task<IReadOnlyList<DietaryTag>> ClassifyDietaryTagsAsync(Recipe recipe);

    /// <summary>
    /// Resolves a colloquial or unmappable measure string to a canonical quantity and unit.
    /// </summary>
    Task<UomResolutionResult> ResolveUomAsync(string colloquialQuantity, string ingredientName);

    /// <summary>
    /// Reviews a generated meal plan and optimizes slot assignments for variety and waste reduction.
    /// </summary>
    Task<IReadOnlyList<MealPlanSlotCandidate>> OptimizeMealPlanAsync(
        IReadOnlyList<MealPlanSlotCandidate> candidates, IReadOnlyList<InventoryItem> expiringItems,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests substitutions for missing ingredients in a near-match recipe.
    /// </summary>
    Task<IReadOnlyList<SubstitutionSuggestion>> SuggestSubstitutionsAsync(
        Recipe recipe, IReadOnlyList<MissingIngredient> missing, IReadOnlyList<InventoryItem> pantry,
        CancellationToken cancellationToken = default);
}

/// <summary>A missing ingredient for substitution suggestions.</summary>
public sealed class MissingIngredient
{
    public string CanonicalIngredientName { get; init; } = string.Empty;
    public decimal RequiredQuantity { get; init; }
    public string RequiredUom { get; init; } = string.Empty;
}

/// <summary>A Claude-suggested substitution for a missing ingredient.</summary>
public sealed class SubstitutionSuggestion
{
    public ClaudeConfidence Confidence { get; init; }
    public string MissingIngredientName { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string SuggestedSubstitute { get; init; } = string.Empty;
}
