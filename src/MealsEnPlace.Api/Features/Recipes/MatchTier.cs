namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Classifies the quality of a recipe match against the user's current inventory.
/// </summary>
public enum MatchTier
{
    /// <summary>All ingredients present. MatchScore == 1.0.</summary>
    FullMatch,

    /// <summary>Most ingredients present (>= 0.75). Claude suggests substitutions.</summary>
    NearMatch,

    /// <summary>At least half of ingredients present (>= 0.5).</summary>
    PartialMatch
}
