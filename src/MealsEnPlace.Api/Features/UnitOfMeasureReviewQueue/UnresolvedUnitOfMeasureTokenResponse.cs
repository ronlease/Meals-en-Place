namespace MealsEnPlace.Api.Features.UnitOfMeasureReviewQueue;

/// <summary>
/// A single row in the unit-of-measure review queue: a unit token that could
/// not be resolved deterministically during ingest and needs the user to
/// decide how to map it (or ignore it).
/// </summary>
public sealed class UnresolvedUnitOfMeasureTokenResponse
{
    /// <summary>Running count of occurrences across ingest runs.</summary>
    public int Count { get; init; }

    /// <summary>First time this token was encountered, UTC.</summary>
    public DateTime FirstSeenAt { get; init; }

    /// <summary>The queue row's primary key.</summary>
    public Guid Id { get; init; }

    /// <summary>Most recent time this token was encountered, UTC.</summary>
    public DateTime LastSeenAt { get; init; }

    /// <summary>Representative ingredient context from the most recent occurrence.</summary>
    public string SampleIngredientContext { get; init; } = string.Empty;

    /// <summary>Representative original measure string from the most recent occurrence.</summary>
    public string SampleMeasureString { get; init; } = string.Empty;

    /// <summary>The extracted unit token that could not be resolved.</summary>
    public string UnitToken { get; init; } = string.Empty;
}
