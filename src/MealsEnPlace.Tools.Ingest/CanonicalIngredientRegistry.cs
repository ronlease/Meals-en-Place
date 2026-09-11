using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// In-memory cache + upsert surface for <see cref="CanonicalIngredient"/>
/// rows derived from the Kaggle dataset's NER column. Preloads existing
/// canonicals at startup, keyed by lowercased name, so per-recipe NER tokens
/// resolve against the cache without per-token DB queries. New canonicals
/// are added to the DbContext (flushed by the batched writer) and also
/// cached for subsequent lookups in the same batch.
/// <para>
/// Also exposes a heuristic mapper that links a raw ingredient string to
/// the most specific NER token it contains, so <c>RecipeIngredient</c>
/// rows can be persisted with the right <c>CanonicalIngredientId</c> FK.
/// </para>
/// <para>
/// Instance is NOT thread-safe; ingest is single-threaded.
/// </para>
/// </summary>
internal sealed class CanonicalIngredientRegistry
{
    private readonly Dictionary<string, Guid> _byLowerName;
    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly Guid _defaultUnitOfMeasureId;

    private CanonicalIngredientRegistry(
        MealsEnPlaceDbContext dbContext,
        Guid defaultUnitOfMeasureId,
        Dictionary<string, Guid> byLowerName)
    {
        _byLowerName = byLowerName;
        _dbContext = dbContext;
        _defaultUnitOfMeasureId = defaultUnitOfMeasureId;
    }

    /// <summary>
    /// Cumulative count of new CanonicalIngredient rows the registry has
    /// inserted during this ingest run. Consumers read this for summary
    /// reporting.
    /// </summary>
    public int NewRowsCreated { get; private set; }

    /// <summary>
    /// Loads existing CanonicalIngredient rows and constructs a registry.
    /// Call once at the start of an ingest run.
    /// </summary>
    public static async Task<CanonicalIngredientRegistry> LoadAsync(
        MealsEnPlaceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var defaultUnitOfMeasure = await dbContext.UnitsOfMeasure
            .AsNoTracking()
            .FirstAsync(u => u.Abbreviation == IngestConstants.DefaultCanonicalIngredientUnitOfMeasureAbbreviation,
                cancellationToken);

        var existing = await dbContext.CanonicalIngredients
            .AsNoTracking()
            .Select(ci => new { ci.Id, ci.Name })
            .ToListAsync(cancellationToken);

        var byLowerName = new Dictionary<string, Guid>(
            IngestConstants.CanonicalIngredientDedupCacheInitialCapacity,
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in existing)
        {
            byLowerName.TryAdd(row.Name, row.Id);
        }

        return new CanonicalIngredientRegistry(dbContext, defaultUnitOfMeasure.Id, byLowerName);
    }

    // Matches CanonicalIngredient.Name's HasMaxLength(200). Kaggle NER tokens
    // occasionally exceed this (e.g. long descriptive phrases); truncate before
    // lookup and insert so the dedupe cache keys agree with what lands in the
    // unique index.
    private const int CanonicalIngredientNameMaxLength = 200;

    /// <summary>
    /// Ensures a <see cref="CanonicalIngredient"/> exists for the given
    /// NER token. Returns the existing id when cached; inserts a new row
    /// (into the DbContext, not yet saved) when novel. Normalization via
    /// <see cref="NerTokenNormalizer.Normalize"/> is applied defensively so
    /// that callers which bypass the per-row cleaning step in Program.cs
    /// cannot introduce junk rows. A token that normalizes to a rejection
    /// falls back to the "unknown" canonical exactly as degenerate empty
    /// input does today. Over-length tokens are truncated to
    /// <see cref="CanonicalIngredientNameMaxLength"/> after normalization.
    /// </summary>
    public Guid GetOrCreate(string nerToken)
    {
        var normalizationResult = NerTokenNormalizer.Normalize(nerToken);

        // Rejected tokens (empty after cleanup, no letters, stopwords only) and
        // genuinely degenerate input both fall back to "unknown" so FK constraints
        // remain satisfied. Program.cs filters these out before calling GetOrCreate
        // in normal flow; this path is the safety net for unexpected callers.
        string trimmed;
        if (normalizationResult.IsRejected)
        {
            trimmed = "unknown";
        }
        else
        {
            trimmed = normalizationResult.NormalizedValue!;
        }

        if (trimmed.Length > CanonicalIngredientNameMaxLength)
        {
            // TrimEnd so a cut at a word boundary cannot leave a trailing space
            // that would defeat the case-insensitive dedupe against the same prefix.
            trimmed = trimmed[..CanonicalIngredientNameMaxLength].TrimEnd();
        }

        if (_byLowerName.TryGetValue(trimmed, out var existingId))
        {
            return existingId;
        }

        var newRow = new CanonicalIngredient
        {
            Category = IngestConstants.DefaultCanonicalIngredientCategory,
            DefaultUnitOfMeasureId = _defaultUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = trimmed
        };

        _dbContext.CanonicalIngredients.Add(newRow);
        _byLowerName[trimmed] = newRow.Id;
        NewRowsCreated++;

        return newRow.Id;
    }

    /// <summary>
    /// Picks the most specific NER token that appears as a whole-word
    /// substring of <paramref name="rawIngredient"/>. Returns null when
    /// no NER token is contained in the raw string.
    /// </summary>
    /// <remarks>
    /// "Most specific" = longest NER token by character count, tie-broken
    /// by first occurrence. "Whole-word substring" means the token's edges
    /// sit on a word boundary so e.g. "can" does not match "pecan".
    /// </remarks>
    public static string? PickBestNerMatch(string rawIngredient, IReadOnlyList<string> nerTokens)
    {
        if (string.IsNullOrWhiteSpace(rawIngredient) || nerTokens.Count == 0)
        {
            return null;
        }

        var rawLower = rawIngredient.ToLowerInvariant();
        string? best = null;
        var bestLength = 0;

        foreach (var token in nerTokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!ContainsWholeWord(rawLower, token.ToLowerInvariant()))
            {
                continue;
            }

            if (token.Length > bestLength)
            {
                best = token;
                bestLength = token.Length;
            }
        }

        return best;
    }

    private static bool ContainsWholeWord(string source, string word)
    {
        var index = source.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            var startOk = index == 0 || !char.IsLetter(source[index - 1]);
            var endOk = index + word.Length == source.Length
                        || !char.IsLetter(source[index + word.Length]);

            if (startOk && endOk)
            {
                return true;
            }

            index = source.IndexOf(word, index + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
