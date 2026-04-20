using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Ingest-mode unit-of-measure resolver that loads the <see cref="UnitOfMeasure"/>
/// and <see cref="UnitOfMeasureAlias"/> tables into in-memory dictionaries at
/// construction time so per-ingredient resolution is O(1) and avoids ~30M
/// database round-trips across a full 1.64M-row ingest run.
/// <para>
/// Applies the same deterministic resolution order as the runtime
/// <see cref="UnitOfMeasureNormalizationService"/>: abbreviation / name match, then
/// alias match, then count-with-ingredient-noun fallback. When no step
/// matches, writes (or upserts) an <see cref="UnresolvedUnitOfMeasureToken"/>
/// row via the supplied DbContext instead of invoking Claude.
/// </para>
/// <para>
/// Instance is NOT thread-safe; ingest is single-threaded by design.
/// </para>
/// </summary>
internal sealed class InMemoryUnitOfMeasureResolver
{
    private readonly Dictionary<string, (Guid Id, string Abbreviation)> _aliasByText;
    private readonly (Guid Id, string Abbreviation) _eachUnitOfMeasure;
    private readonly MealsEnPlaceDbContext _dbContext;

    // Tracks queue-row entities we've already added within this DbContext
    // scope so repeat tokens increment in-memory instead of re-inserting.
    // Cleared by the caller (via ChangeTracker.Clear) on batch flush.
    private readonly Dictionary<string, UnresolvedUnitOfMeasureToken> _pendingQueueRowByToken;
    private readonly Dictionary<string, (Guid Id, string Abbreviation)> _unitOfMeasureByAbbreviationOrName;

    private InMemoryUnitOfMeasureResolver(
        MealsEnPlaceDbContext dbContext,
        Dictionary<string, (Guid Id, string Abbreviation)> unitOfMeasureByAbbreviationOrName,
        Dictionary<string, (Guid Id, string Abbreviation)> aliasByText,
        (Guid Id, string Abbreviation) eachUnitOfMeasure)
    {
        _aliasByText = aliasByText;
        _dbContext = dbContext;
        _eachUnitOfMeasure = eachUnitOfMeasure;
        _pendingQueueRowByToken = new Dictionary<string, UnresolvedUnitOfMeasureToken>(
            IngestConstants.UnresolvedTokenDedupCacheInitialCapacity,
            StringComparer.Ordinal);
        _unitOfMeasureByAbbreviationOrName = unitOfMeasureByAbbreviationOrName;
    }

    /// <summary>
    /// Loads the <see cref="UnitOfMeasure"/> and <see cref="UnitOfMeasureAlias"/>
    /// tables into memory and constructs a resolver. Call once at the start of
    /// an ingest run.
    /// </summary>
    public static async Task<InMemoryUnitOfMeasureResolver> LoadAsync(
        MealsEnPlaceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var unitsOfMeasure = await dbContext.UnitsOfMeasure.AsNoTracking().ToListAsync(cancellationToken);

        // Build case-insensitive lookup that accepts either Abbreviation or Name
        // (and their plural-trimmed forms), mirroring the runtime service.
        var unitOfMeasureByToken = new Dictionary<string, (Guid Id, string Abbreviation)>(
            unitsOfMeasure.Count * 4, StringComparer.OrdinalIgnoreCase);

        foreach (var unitOfMeasure in unitsOfMeasure)
        {
            var entry = (unitOfMeasure.Id, unitOfMeasure.Abbreviation);
            TryAdd(unitOfMeasureByToken, unitOfMeasure.Abbreviation, entry);
            TryAdd(unitOfMeasureByToken, unitOfMeasure.Abbreviation.TrimEnd('s'), entry);
            TryAdd(unitOfMeasureByToken, unitOfMeasure.Name, entry);
            TryAdd(unitOfMeasureByToken, unitOfMeasure.Name.TrimEnd('s'), entry);
        }

        var aliases = await dbContext.UnitOfMeasureAliases
            .AsNoTracking()
            .Include(a => a.UnitOfMeasure)
            .ToListAsync(cancellationToken);

        // Alias lookup uses case-insensitive matching (the C# service does
        // .ToLower() on both sides). Duplicate aliases differing only in
        // case are possible in this table -- first writer wins in the cache
        // and the tool does not need finer arbitration at ingest time.
        var aliasByText = new Dictionary<string, (Guid Id, string Abbreviation)>(
            aliases.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var alias in aliases)
        {
            var unitOfMeasure = alias.UnitOfMeasure!;
            aliasByText.TryAdd(alias.Alias, (unitOfMeasure.Id, unitOfMeasure.Abbreviation));
        }

        var eachUnitOfMeasure = unitsOfMeasure.First(
            u => u.Abbreviation == IngestConstants.DefaultCanonicalIngredientUnitOfMeasureAbbreviation);
        var eachEntry = (eachUnitOfMeasure.Id, eachUnitOfMeasure.Abbreviation);

        return new InMemoryUnitOfMeasureResolver(dbContext, unitOfMeasureByToken, aliasByText, eachEntry);
    }

    /// <summary>
    /// Called by the caller after a batch flush + <c>ChangeTracker.Clear()</c>
    /// so the resolver drops its per-batch pending-queue-row dedup map.
    /// Without this, a token upserted in a prior batch would be re-added to
    /// the tracker as a new entity in the next batch.
    /// </summary>
    public void ResetPerBatchState() => _pendingQueueRowByToken.Clear();

    /// <summary>
    /// Attempts deterministic resolution. On miss, writes (or increments) an
    /// <see cref="UnresolvedUnitOfMeasureToken"/> row and returns a deferred
    /// result. The <see cref="MealsEnPlaceDbContext"/> is mutated but
    /// <c>SaveChangesAsync</c> is NOT called here; the batched writer owns
    /// flushing.
    /// </summary>
    public IngestUnitOfMeasureResolution NormalizeOrDefer(string measureString, string ingredientName)
    {
        var (quantity, unitToken) = UnitOfMeasureTokenParser.Parse(measureString);

        // Step 1: abbreviation / name lookup. Mirror the runtime service by
        // trying the raw token and a plural-stripped variant so "cups" matches
        // a seeded "cup" row without requiring a dedicated alias.
        if (!string.IsNullOrWhiteSpace(unitToken)
            && (_unitOfMeasureByAbbreviationOrName.TryGetValue(unitToken, out var unitOfMeasureHit)
                || _unitOfMeasureByAbbreviationOrName.TryGetValue(unitToken.TrimEnd('s'), out unitOfMeasureHit)))
        {
            return IngestUnitOfMeasureResolution.Resolved(quantity, unitOfMeasureHit.Id, unitOfMeasureHit.Abbreviation);
        }

        // Step 2: alias lookup. Aliases are stored verbatim so "cups" would
        // only match an explicitly-seeded "cups" alias.
        if (!string.IsNullOrWhiteSpace(unitToken)
            && _aliasByText.TryGetValue(unitToken, out var aliasHit))
        {
            return IngestUnitOfMeasureResolution.Resolved(quantity, aliasHit.Id, aliasHit.Abbreviation);
        }

        // Step 3: count-with-ingredient-noun fallback.
        if (quantity > 0m && !string.IsNullOrWhiteSpace(unitToken))
        {
            return IngestUnitOfMeasureResolution.Resolved(
                quantity, _eachUnitOfMeasure.Id, _eachUnitOfMeasure.Abbreviation);
        }

        // Step 4: defer. Upsert the queue row.
        if (!string.IsNullOrWhiteSpace(unitToken))
        {
            UpsertDeferredToken(unitToken, measureString, ingredientName);
        }

        return IngestUnitOfMeasureResolution.Deferred(quantity);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    // Column caps from UnresolvedUnitOfMeasureTokenConfiguration. Kaggle
    // measure strings occasionally exceed these (long descriptive phrases,
    // embedded instructions). Truncate before lookup and write so the dedupe
    // key stays consistent and SaveChanges never trips a 22001.
    private const int SampleStringMaxLength = 500;
    private const int UnitTokenMaxLength = 100;

    private void UpsertDeferredToken(string unitToken, string measureString, string ingredientName)
    {
        var safeUnitToken = Truncate(unitToken, UnitTokenMaxLength);
        var safeMeasureString = Truncate(measureString, SampleStringMaxLength);
        var safeIngredientName = Truncate(ingredientName, SampleStringMaxLength);
        var now = DateTime.UtcNow;

        if (_pendingQueueRowByToken.TryGetValue(safeUnitToken, out var pending))
        {
            pending.Count += 1;
            pending.LastSeenAt = now;
            pending.SampleMeasureString = safeMeasureString;
            pending.SampleIngredientContext = safeIngredientName;
            return;
        }

        // Not in our per-batch cache. Check the DB for an existing row.
        var existing = _dbContext.UnresolvedUnitOfMeasureTokens
            .Local.FirstOrDefault(t => t.UnitToken == safeUnitToken)
            ?? _dbContext.UnresolvedUnitOfMeasureTokens.FirstOrDefault(t => t.UnitToken == safeUnitToken);

        if (existing is not null)
        {
            existing.Count += 1;
            existing.LastSeenAt = now;
            existing.SampleMeasureString = safeMeasureString;
            existing.SampleIngredientContext = safeIngredientName;
            _pendingQueueRowByToken[safeUnitToken] = existing;
            return;
        }

        var row = new UnresolvedUnitOfMeasureToken
        {
            Count = 1,
            FirstSeenAt = now,
            Id = Guid.NewGuid(),
            LastSeenAt = now,
            SampleIngredientContext = safeIngredientName,
            SampleMeasureString = safeMeasureString,
            UnitToken = safeUnitToken
        };

        _dbContext.UnresolvedUnitOfMeasureTokens.Add(row);
        _pendingQueueRowByToken[safeUnitToken] = row;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static void TryAdd(
        Dictionary<string, (Guid Id, string Abbreviation)> dict,
        string key,
        (Guid Id, string Abbreviation) value)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            dict.TryAdd(key, value);
        }
    }
}
