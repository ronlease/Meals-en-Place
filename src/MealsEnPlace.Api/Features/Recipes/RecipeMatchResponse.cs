namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Top-level response from the recipe matching endpoint.
/// </summary>
public sealed class RecipeMatchResponse
{
    /// <summary>
    /// True when the Claude-backed feasibility / substitution pass ran against
    /// the near-match candidates. False when no Claude API key is configured
    /// (MEP-032): the deterministic ranking is still authoritative but the UI
    /// should surface a subtle note that AI-suggested substitutions are
    /// unavailable.
    /// </summary>
    public bool ClaudeFeasibilityApplied { get; init; }

    /// <summary>All ingredients present (MatchScore == 1.0), sorted by FinalScore desc.</summary>
    public IReadOnlyList<RecipeMatchDto> FullMatches { get; init; } = [];

    /// <summary>Most ingredients present (>= 0.75), with substitution suggestions.</summary>
    public IReadOnlyList<RecipeMatchDto> NearMatches { get; init; } = [];

    /// <summary>At least half present (>= 0.5).</summary>
    public IReadOnlyList<RecipeMatchDto> PartialMatches { get; init; } = [];
}
