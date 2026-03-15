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
    Task<UomResolutionResult> ResolveUomAsync(string colloquialQuantity, string ingredientName);
}
