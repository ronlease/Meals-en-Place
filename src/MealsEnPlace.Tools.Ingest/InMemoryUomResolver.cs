using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Ingest-mode UOM resolver that loads the UnitOfMeasure and
/// UnitOfMeasureAlias tables into in-memory dictionaries at construction
/// time so per-ingredient resolution is O(1) and avoids ~30M database
/// round-trips across a full 1.64M-row ingest run.
/// <para>
/// Applies the same deterministic resolution order as the runtime
/// <see cref="UomNormalizationService"/>: abbreviation / name match,
/// then alias match, then count-with-ingredient-noun fallback. When no
/// step matches, writes (or upserts) an <see cref="UnresolvedUomToken"/>
/// row via the supplied DbContext instead of invoking Claude.
/// </para>
/// <para>
/// Instance is NOT thread-safe; ingest is single-threaded by design.
/// </para>
/// </summary>
internal sealed class InMemoryUomResolver
{
    private readonly Dictionary<string, (Guid Id, string Abbreviation)> _aliasByText;
    private readonly (Guid Id, string Abbreviation) _eachUom;
    private readonly MealsEnPlaceDbContext _dbContext;

    // Tracks queue-row entities we've already added within this DbContext
    // scope so repeat tokens increment in-memory instead of re-inserting.
    // Cleared by the caller (via ChangeTracker.Clear) on batch flush.
    private readonly Dictionary<string, UnresolvedUomToken> _pendingQueueRowByToken;
    private readonly Dictionary<string, (Guid Id, string Abbreviation)> _uomByAbbreviationOrName;

    private InMemoryUomResolver(
        MealsEnPlaceDbContext dbContext,
        Dictionary<string, (Guid Id, string Abbreviation)> uomByAbbreviationOrName,
        Dictionary<string, (Guid Id, string Abbreviation)> aliasByText,
        (Guid Id, string Abbreviation) eachUom)
    {
        _aliasByText = aliasByText;
        _dbContext = dbContext;
        _eachUom = eachUom;
        _pendingQueueRowByToken = new Dictionary<string, UnresolvedUomToken>(
            IngestConstants.UnresolvedTokenDedupCacheInitialCapacity,
            StringComparer.Ordinal);
        _uomByAbbreviationOrName = uomByAbbreviationOrName;
    }

    /// <summary>
    /// Loads the UOM and alias tables into memory and constructs a resolver.
    /// Call once at the start of an ingest run.
    /// </summary>
    public static async Task<InMemoryUomResolver> LoadAsync(
        MealsEnPlaceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var uoms = await dbContext.UnitsOfMeasure.AsNoTracking().ToListAsync(cancellationToken);

        // Build case-insensitive lookup that accepts either Abbreviation or Name
        // (and their plural-trimmed forms), mirroring the runtime service.
        var uomByToken = new Dictionary<string, (Guid Id, string Abbreviation)>(
            uoms.Count * 4, StringComparer.OrdinalIgnoreCase);

        foreach (var uom in uoms)
        {
            var entry = (uom.Id, uom.Abbreviation);
            TryAdd(uomByToken, uom.Abbreviation, entry);
            TryAdd(uomByToken, uom.Abbreviation.TrimEnd('s'), entry);
            TryAdd(uomByToken, uom.Name, entry);
            TryAdd(uomByToken, uom.Name.TrimEnd('s'), entry);
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
            var uom = alias.UnitOfMeasure!;
            aliasByText.TryAdd(alias.Alias, (uom.Id, uom.Abbreviation));
        }

        var eachUom = uoms.First(u => u.Abbreviation == IngestConstants.DefaultCanonicalIngredientUomAbbreviation);
        var eachEntry = (eachUom.Id, eachUom.Abbreviation);

        return new InMemoryUomResolver(dbContext, uomByToken, aliasByText, eachEntry);
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
    /// <see cref="UnresolvedUomToken"/> row and returns a deferred result.
    /// The <see cref="MealsEnPlaceDbContext"/> is mutated but
    /// <c>SaveChangesAsync</c> is NOT called here; the batched writer owns
    /// flushing.
    /// </summary>
    public IngestUomResolution NormalizeOrDefer(string measureString, string ingredientName)
    {
        var (quantity, unitToken) = UomTokenParser.Parse(measureString);

        // Step 1: abbreviation / name lookup. Mirror the runtime service by
        // trying the raw token and a plural-stripped variant so "cups" matches
        // a seeded "cup" row without requiring a dedicated alias.
        if (!string.IsNullOrWhiteSpace(unitToken)
            && (_uomByAbbreviationOrName.TryGetValue(unitToken, out var uomHit)
                || _uomByAbbreviationOrName.TryGetValue(unitToken.TrimEnd('s'), out uomHit)))
        {
            return IngestUomResolution.Resolved(quantity, uomHit.Id, uomHit.Abbreviation);
        }

        // Step 2: alias lookup. Aliases are stored verbatim so "cups" would
        // only match an explicitly-seeded "cups" alias.
        if (!string.IsNullOrWhiteSpace(unitToken)
            && _aliasByText.TryGetValue(unitToken, out var aliasHit))
        {
            return IngestUomResolution.Resolved(quantity, aliasHit.Id, aliasHit.Abbreviation);
        }

        // Step 3: count-with-ingredient-noun fallback.
        if (quantity > 0m && !string.IsNullOrWhiteSpace(unitToken))
        {
            return IngestUomResolution.Resolved(quantity, _eachUom.Id, _eachUom.Abbreviation);
        }

        // Step 4: defer. Upsert the queue row.
        if (!string.IsNullOrWhiteSpace(unitToken))
        {
            UpsertDeferredToken(unitToken, measureString, ingredientName);
        }

        return IngestUomResolution.Deferred(quantity);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void UpsertDeferredToken(string unitToken, string measureString, string ingredientName)
    {
        var now = DateTime.UtcNow;

        if (_pendingQueueRowByToken.TryGetValue(unitToken, out var pending))
        {
            pending.Count += 1;
            pending.LastSeenAt = now;
            pending.SampleMeasureString = measureString;
            pending.SampleIngredientContext = ingredientName;
            return;
        }

        // Not in our per-batch cache. Check the DB for an existing row.
        var existing = _dbContext.UnresolvedUomTokens
            .Local.FirstOrDefault(t => t.UnitToken == unitToken)
            ?? _dbContext.UnresolvedUomTokens.FirstOrDefault(t => t.UnitToken == unitToken);

        if (existing is not null)
        {
            existing.Count += 1;
            existing.LastSeenAt = now;
            existing.SampleMeasureString = measureString;
            existing.SampleIngredientContext = ingredientName;
            _pendingQueueRowByToken[unitToken] = existing;
            return;
        }

        var row = new UnresolvedUomToken
        {
            Count = 1,
            FirstSeenAt = now,
            Id = Guid.NewGuid(),
            LastSeenAt = now,
            SampleIngredientContext = ingredientName,
            SampleMeasureString = measureString,
            UnitToken = unitToken
        };

        _dbContext.UnresolvedUomTokens.Add(row);
        _pendingQueueRowByToken[unitToken] = row;
    }

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

/// <summary>
/// Result of an <see cref="InMemoryUomResolver.NormalizeOrDefer"/> call.
/// Either resolved to a canonical <c>UnitOfMeasure</c>, or deferred to the
/// review queue.
/// </summary>
internal sealed class IngestUomResolution
{
    public string? UomAbbreviation { get; init; }

    public Guid? UomId { get; init; }

    public decimal Quantity { get; init; }

    public bool WasDeferred { get; init; }

    public static IngestUomResolution Deferred(decimal quantity) =>
        new() { Quantity = quantity, WasDeferred = true };

    public static IngestUomResolution Resolved(decimal quantity, Guid uomId, string uomAbbreviation) =>
        new()
        {
            Quantity = quantity,
            UomAbbreviation = uomAbbreviation,
            UomId = uomId,
            WasDeferred = false
        };
}
