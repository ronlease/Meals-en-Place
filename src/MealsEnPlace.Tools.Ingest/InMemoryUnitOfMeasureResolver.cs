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
/// alias match, then count-with-ingredient-noun fallback (MEP-063: only when
/// the parser confirms the leftover text is the ingredient noun; a leading
/// word such as "heaping" is tried as a unit candidate rather than assumed to
/// be a count). When no step matches, writes (or upserts) an <see cref="UnresolvedUnitOfMeasureToken"/>
/// row via the supplied DbContext instead of invoking Claude.
/// </para>
/// <para>
/// Instance is NOT thread-safe; ingest is single-threaded by design.
/// </para>
/// </summary>
internal sealed class InMemoryUnitOfMeasureResolver
{
    private readonly Dictionary<string, (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType)> _aliasByText;
    private readonly (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType) _eachUnitOfMeasure;
    private readonly MealsEnPlaceDbContext _dbContext;

    // Tracks queue-row entities we've already added within this DbContext
    // scope so repeat tokens increment in-memory instead of re-inserting.
    // Cleared by the caller (via ChangeTracker.Clear) on batch flush.
    private readonly Dictionary<string, UnresolvedUnitOfMeasureToken> _pendingQueueRowByToken;
    private readonly Dictionary<string, (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType)> _unitOfMeasureByAbbreviationOrName;

    private InMemoryUnitOfMeasureResolver(
        MealsEnPlaceDbContext dbContext,
        Dictionary<string, (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType)> unitOfMeasureByAbbreviationOrName,
        Dictionary<string, (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType)> aliasByText,
        (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType) eachUnitOfMeasure)
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
        var unitOfMeasureByToken = new Dictionary<string, (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType)>(
            unitsOfMeasure.Count * 4, StringComparer.OrdinalIgnoreCase);

        foreach (var unitOfMeasure in unitsOfMeasure)
        {
            var entry = (unitOfMeasure.Id, unitOfMeasure.Abbreviation, unitOfMeasure.UnitOfMeasureType);
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
        var aliasByText = new Dictionary<string, (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType)>(
            aliases.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var alias in aliases)
        {
            var unitOfMeasure = alias.UnitOfMeasure!;
            aliasByText.TryAdd(alias.Alias, (unitOfMeasure.Id, unitOfMeasure.Abbreviation, unitOfMeasure.UnitOfMeasureType));
        }

        var eachUnitOfMeasure = unitsOfMeasure.First(
            u => u.Abbreviation == IngestConstants.DefaultCanonicalIngredientUnitOfMeasureAbbreviation);
        var eachEntry = (eachUnitOfMeasure.Id, eachUnitOfMeasure.Abbreviation, eachUnitOfMeasure.UnitOfMeasureType);

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
        var parsed = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, ingredientName);

        // Step 1: abbreviation / name lookup over each candidate token. Mirror
        // the runtime service by trying the raw token and a plural-stripped
        // variant so "cups" matches a seeded "cup" row without an alias.
        foreach (var candidate in parsed.UnitTokenCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (_unitOfMeasureByAbbreviationOrName.TryGetValue(candidate, out var unitOfMeasureHit)
                || _unitOfMeasureByAbbreviationOrName.TryGetValue(candidate.TrimEnd('s'), out unitOfMeasureHit))
            {
                return IngestUnitOfMeasureResolution.Resolved(
                    parsed.Quantity,
                    unitOfMeasureHit.Id,
                    unitOfMeasureHit.Abbreviation,
                    unitOfMeasureHit.UnitOfMeasureType);
            }
        }

        // Step 2: alias lookup. Aliases are stored verbatim so "cups" would
        // only match an explicitly-seeded "cups" alias.
        foreach (var candidate in parsed.UnitTokenCandidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate)
                && _aliasByText.TryGetValue(candidate, out var aliasHit))
            {
                return IngestUnitOfMeasureResolution.Resolved(
                    parsed.Quantity, aliasHit.Id, aliasHit.Abbreviation, aliasHit.UnitOfMeasureType);
            }
        }

        // Step 3: count-with-ingredient-noun fallback. Only when the parser
        // confirmed the remaining text is the ingredient noun itself.
        if (parsed.Quantity > 0m && parsed.IsCountWithIngredientNoun)
        {
            return IngestUnitOfMeasureResolution.Resolved(
                parsed.Quantity,
                _eachUnitOfMeasure.Id,
                _eachUnitOfMeasure.Abbreviation,
                _eachUnitOfMeasure.UnitOfMeasureType);
        }

        // Step 4: defer. Upsert the queue row.
        if (!string.IsNullOrWhiteSpace(parsed.DeferralUnitToken))
        {
            UpsertDeferredToken(parsed.DeferralUnitToken, measureString, ingredientName);
        }

        return IngestUnitOfMeasureResolution.Deferred(parsed.Quantity, parsed.DeferralUnitToken);
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
        Dictionary<string, (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType)> dict,
        string key,
        (Guid Id, string Abbreviation, UnitOfMeasureType UnitOfMeasureType) value)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            dict.TryAdd(key, value);
        }
    }
}
