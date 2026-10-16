using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// The result of a <see cref="IUnitOfMeasureNormalizationService.NormalizeAsync"/> call.
/// Describes the resolved quantity, unit, confidence, and whether Claude was invoked.
/// </summary>
public sealed class NormalizationResult
{
    /// <summary>
    /// Confidence level of the resolution.
    /// For deterministic lookups this is always <see cref="ClaudeConfidence.High"/>.
    /// For Claude resolutions this reflects <see cref="UnitOfMeasureResolutionResult.Confidence"/>.
    /// </summary>
    public ClaudeConfidence Confidence { get; init; }

    /// <summary>
    /// True when the measure string is a junk line ("None", "-", "to", "or") rather than an
    /// ingredient quantity. Never queued and never sent to Claude; the caller should
    /// treat the line as having no usable quantity.
    /// </summary>
    public bool IsJunkLine { get; init; }

    /// <summary>
    /// True when the line is a to-taste seasoning ("salt to taste", "salt and pepper").
    /// Resolved as quantity 0 in "ea"; the caller stores it as resolved with the original
    /// line in Notes. Never queued.
    /// </summary>
    public bool IsToTaste { get; init; }

    /// <summary>
    /// Optional notes from a Claude resolution explaining assumptions or flagging uncertainty.
    /// Null for deterministic resolutions.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>The resolved numeric quantity in <see cref="UnitOfMeasureAbbreviation"/> units.</summary>
    public decimal Quantity { get; init; }

    /// <summary>The abbreviation of the resolved canonical unit (e.g., "g", "ml", "ea", "cup").</summary>
    public string UnitOfMeasureAbbreviation { get; init; } = string.Empty;

    /// <summary>
    /// The <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Id"/> of the resolved unit.
    /// <see cref="Guid.Empty"/> when Claude could not resolve to a known unit
    /// or when the ingredient was deferred to the review queue.
    /// </summary>
    public Guid UnitOfMeasureId { get; init; }

    /// <summary>
    /// True when Claude was invoked to resolve the measure string.
    /// False when the resolution was performed deterministically or the
    /// ingredient was deferred to the review queue.
    /// </summary>
    public bool WasClaudeResolved { get; init; }

    /// <summary>
    /// True when the ingredient was deferred to the
    /// <see cref="MealsEnPlace.Api.Models.Entities.UnresolvedUnitOfMeasureToken"/> review
    /// queue because ingest mode was set and deterministic resolution failed.
    /// The caller should persist the ingredient in an unresolved state until
    /// the user decides how to map the token.
    /// </summary>
    public bool WasDeferredToQueue { get; init; }
}

/// <summary>
/// Normalizes raw measure strings from recipe imports and inventory entries into
/// canonical units tracked in the unit of measure conversion table.
/// <para>
/// Resolution order:
/// <list type="number">
///   <item><description>Parse a numeric quantity from the measure string and derive candidate unit tokens from the leading words of the remainder (mixed numbers such as "1 1/2 cups" and "1-1/2 cups" are summed, ranges such as "2-3 tbsp" keep the lower bound, and junk words and size descriptors are skipped).</description></item>
///   <item><description>Junk lines ("None", "-", "to", "or") are returned as <see cref="NormalizationResult.IsJunkLine"/>: never queued, Claude never invoked. To-taste lines ("salt", "salt and pepper", "salt to taste") resolve to quantity 0 in "ea" flagged <see cref="NormalizationResult.IsToTaste"/>.</description></item>
///   <item><description>Butter or margarine sticks ("2 sticks butter") resolve to grams at 113 g per stick before unit and alias lookup.</description></item>
///   <item><description>A pinch or dash with no number ("pinch of salt", "a dash of pepper") resolves to quantity 1 of the "pinch" unit, or the "dash" unit when the ingredient is pepper, before unit and alias lookup. Lines with an explicit quantity ("1 pinch salt") are unaffected.</description></item>
///   <item><description>Look up each candidate unit token against <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Abbreviation"/> or <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Name"/> (case-insensitive).</description></item>
///   <item><description>If not found, look up against <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasureAlias"/> to catch dataset variants (e.g., "c.", "Tbsp.", "lbs").</description></item>
///   <item><description>If still not found, a positive quantity parsed, and the remainder is empty, starts with the ingredient noun, or started with a size descriptor, default to "each" (e.g. "4 chicken breasts" for "chicken breast", "1 large yellow onion"). Unrecognized tokens such as "sprigs" are not treated as counts.</description></item>
///   <item><description>If still not found, fall back to Claude via <see cref="IClaudeService.ResolveUnitOfMeasureAsync"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// Container references must be filtered out by <see cref="ContainerReferenceDetector"/> before
/// calling this service — this service does not repeat that check.
/// </para>
/// </summary>
public interface IUnitOfMeasureNormalizationService
{
    /// <summary>
    /// Attempts to normalize <paramref name="measureString"/> to a canonical quantity and unit.
    /// </summary>
    /// <param name="measureString">
    /// The raw measure string to normalize (e.g., "2 cups", "500g", "a knob", "1 head").
    /// Must not contain container references — callers are responsible for pre-filtering.
    /// </param>
    /// <param name="ingredientName">
    /// The ingredient name, used as context when Claude is invoked
    /// (e.g., "butter", "garlic").
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="NormalizationResult"/> with the resolved quantity, unit, and confidence.
    /// When <see cref="NormalizationResult.Confidence"/> is <see cref="ClaudeConfidence.Low"/>,
    /// the caller must surface a prompt to the user rather than applying the result silently.
    /// </returns>
    Task<NormalizationResult> NormalizeAsync(
        string measureString,
        string ingredientName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest-mode normalization. Attempts the same deterministic resolution order
    /// as <see cref="NormalizeAsync"/> (abbreviation / name / alias / count-with-ingredient-noun
    /// fallback), but when no deterministic match is found it writes an
    /// <see cref="MealsEnPlace.Api.Models.Entities.UnresolvedUnitOfMeasureToken"/> row to
    /// the review queue instead of invoking Claude. This preserves Claude quota
    /// during bulk ingest and lets the user decide how to map recurring tokens
    /// in one place. Junk lines, to-taste lines, range fragments, size descriptors and
    /// empty tokens never queue (see <see cref="NormalizationResult.IsJunkLine"/> and
    /// <see cref="NormalizationResult.IsToTaste"/>).
    /// </summary>
    /// <param name="measureString">The raw measure string to normalize.</param>
    /// <param name="ingredientName">The ingredient name, captured as sample context in the queue row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="NormalizationResult"/>. <see cref="NormalizationResult.WasDeferredToQueue"/>
    /// is true when the token was queued; in that case the caller must persist the
    /// ingredient in an unresolved state until the user resolves the token.
    /// </returns>
    Task<NormalizationResult> NormalizeOrDeferAsync(
        string measureString,
        string ingredientName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only preview that runs the deterministic resolution steps only
    /// (abbreviation / name / alias / count-noun) and reports whether a
    /// deterministic match exists. Never invokes Claude and never writes to
    /// the review queue. Intended for dry-run tooling that wants to measure
    /// how many ingredients would resolve vs. be deferred before committing
    /// to a full ingest.
    /// </summary>
    /// <param name="measureString">The raw measure string to preview.</param>
    /// <param name="ingredientName">
    /// The ingredient name, used to recognize a count followed by the ingredient noun
    /// (e.g. "4 chicken breasts"). May be null.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="NormalizationResult"/> on a deterministic match; null
    /// otherwise.
    /// </returns>
    Task<NormalizationResult?> TryResolveDeterministicallyAsync(
        string measureString,
        string? ingredientName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// <inheritdoc cref="IUnitOfMeasureNormalizationService"/>
/// </summary>
public class UnitOfMeasureNormalizationService(
    IClaudeAvailability claudeAvailability,
    IClaudeService claudeService,
    MealsEnPlaceDbContext dbContext) : IUnitOfMeasureNormalizationService
{
    /// <inheritdoc />
    public async Task<NormalizationResult> NormalizeAsync(
        string measureString,
        string ingredientName,
        CancellationToken cancellationToken = default)
    {
        var parsed = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, ingredientName);

        var deterministic = await TryResolveDeterministicallyCoreAsync(parsed, cancellationToken);

        if (deterministic is not null)
        {
            return deterministic;
        }

        // MEP-032: when no Claude API key is configured, route the unresolved
        // token to the review queue instead of calling the Claude fallback.
        if (!await claudeAvailability.IsConfiguredAsync(cancellationToken))
        {
            return await DeferToReviewQueueAsync(
                parsed.Quantity, parsed.DeferralUnitToken, measureString, ingredientName, cancellationToken);
        }

        // Claude fallback for colloquial or unmapped units.
        var claudeResult = await claudeService.ResolveUnitOfMeasureAsync(measureString, ingredientName);

        // Attempt to map the Claude-resolved abbreviation back to a known unit of measure.
        var resolvedUnitOfMeasure = string.IsNullOrWhiteSpace(claudeResult.ResolvedUnitOfMeasure)
            ? null
            : await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Abbreviation.ToLower() == claudeResult.ResolvedUnitOfMeasure.ToLower(),
                    cancellationToken);

        return new NormalizationResult
        {
            Confidence = claudeResult.Confidence,
            Notes = claudeResult.Notes,
            Quantity = claudeResult.ResolvedQuantity,
            UnitOfMeasureAbbreviation = resolvedUnitOfMeasure?.Abbreviation ?? claudeResult.ResolvedUnitOfMeasure,
            UnitOfMeasureId = resolvedUnitOfMeasure?.Id ?? Guid.Empty,
            WasClaudeResolved = true
        };
    }

    /// <inheritdoc />
    public async Task<NormalizationResult> NormalizeOrDeferAsync(
        string measureString,
        string ingredientName,
        CancellationToken cancellationToken = default)
    {
        var parsed = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, ingredientName);

        var deterministic = await TryResolveDeterministicallyCoreAsync(parsed, cancellationToken);

        if (deterministic is not null)
        {
            return deterministic;
        }

        return await DeferToReviewQueueAsync(
            parsed.Quantity, parsed.DeferralUnitToken, measureString, ingredientName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NormalizationResult?> TryResolveDeterministicallyAsync(
        string measureString,
        string? ingredientName,
        CancellationToken cancellationToken = default)
    {
        var parsed = UnitOfMeasureTokenParser.ParseWithIngredientContext(measureString, ingredientName);
        return await TryResolveDeterministicallyCoreAsync(parsed, cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Records the unresolved token on the review queue and returns a
    /// <see cref="NormalizationResult"/> flagged <see cref="NormalizationResult.WasDeferredToQueue"/>.
    /// Used by both the ingest-time deferral path and the MEP-032 no-key fallback
    /// so both surface identical review-queue behavior.
    /// </summary>
    private async Task<NormalizationResult> DeferToReviewQueueAsync(
        decimal parsedQuantity,
        string unitToken,
        string measureString,
        string ingredientName,
        CancellationToken cancellationToken)
    {
        await UpsertUnresolvedTokenAsync(
            unitToken, measureString, ingredientName, cancellationToken);

        return new NormalizationResult
        {
            Confidence = ClaudeConfidence.Low,
            Notes = "Deferred to review queue.",
            Quantity = parsedQuantity,
            UnitOfMeasureAbbreviation = string.Empty,
            UnitOfMeasureId = Guid.Empty,
            WasClaudeResolved = false,
            WasDeferredToQueue = true
        };
    }

    /// <summary>
    /// Runs the deterministic resolution steps (abbreviation / name, alias,
    /// count-with-ingredient-noun) and returns a <see cref="NormalizationResult"/>
    /// on success or null if no step matched. Shared by <see cref="NormalizeAsync"/>
    /// and <see cref="NormalizeOrDeferAsync"/> so both expose identical determinism.
    /// </summary>
    private async Task<NormalizationResult?> TryResolveDeterministicallyCoreAsync(
        MeasureStringParseResult parsed,
        CancellationToken cancellationToken)
    {
        // Step 0a: junk lines are neither units nor ingredients; never queued, never sent to Claude.
        if (parsed.IsJunkLine)
        {
            return new NormalizationResult
            {
                Confidence = ClaudeConfidence.High,
                IsJunkLine = true,
                Quantity = 0m,
                UnitOfMeasureAbbreviation = string.Empty,
                UnitOfMeasureId = Guid.Empty,
                WasClaudeResolved = false
            };
        }

        // Step 0b: to-taste seasoning stores as quantity 0 in "ea" and is never queued.
        if (parsed.IsToTaste)
        {
            var toTasteUnitOfMeasure = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Abbreviation == "ea", cancellationToken);

            return new NormalizationResult
            {
                Confidence = ClaudeConfidence.High,
                IsToTaste = true,
                Quantity = 0m,
                UnitOfMeasureAbbreviation = toTasteUnitOfMeasure?.Abbreviation ?? "ea",
                UnitOfMeasureId = toTasteUnitOfMeasure?.Id ?? Guid.Empty,
                WasClaudeResolved = false
            };
        }

        // Step 0c: ingredient-specific weight per unit (butter stick = 113 g) wins over
        // unit / alias lookup, where "stick" would otherwise map to "ea".
        if (parsed.IngredientSpecificGramsPerUnit is { } gramsPerUnit)
        {
            var gramUnitOfMeasure = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Abbreviation == "g", cancellationToken);

            if (gramUnitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsed.Quantity * gramsPerUnit,
                    UnitOfMeasureAbbreviation = gramUnitOfMeasure.Abbreviation,
                    UnitOfMeasureId = gramUnitOfMeasure.Id,
                    WasClaudeResolved = false
                };
            }
        }

        // Step 0d: a pinch or dash with no number ("pinch of salt", "dash of pepper") is a
        // quantity of 1 in the seeded pinch or dash unit; never queued.
        if (parsed.ImplicitQuantity is { } implicitQuantity
            && parsed.PreferredUnitOfMeasureAbbreviation is { } preferredAbbreviation)
        {
            var preferredUnitOfMeasure = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Abbreviation == preferredAbbreviation, cancellationToken);

            if (preferredUnitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = implicitQuantity,
                    UnitOfMeasureAbbreviation = preferredUnitOfMeasure.Abbreviation,
                    UnitOfMeasureId = preferredUnitOfMeasure.Id,
                    WasClaudeResolved = false
                };
            }
        }

        foreach (var unitToken in parsed.UnitTokenCandidates)
        {
            // Step 1: deterministic lookup by abbreviation / name (with plural stripping).
            var lowerToken = unitToken.ToLower();
            var normalizedToken = lowerToken.TrimEnd('s');
            var unitOfMeasure = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Abbreviation.ToLower() == lowerToken
                         || u.Abbreviation.ToLower() == normalizedToken
                         || u.Name.ToLower() == lowerToken
                         || u.Name.ToLower() == normalizedToken,
                    cancellationToken);

            if (unitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsed.Quantity,
                    UnitOfMeasureAbbreviation = unitOfMeasure.Abbreviation,
                    UnitOfMeasureId = unitOfMeasure.Id,
                    WasClaudeResolved = false
                };
            }

            // Step 2: alias-table lookup for dataset variants (e.g. "c.", "Tbsp.", "lbs").
            var aliasedUnitOfMeasure = await dbContext.UnitOfMeasureAliases
                .AsNoTracking()
                .Where(a => a.Alias.ToLower() == lowerToken)
                .Select(a => a.UnitOfMeasure!)
                .FirstOrDefaultAsync(cancellationToken);

            if (aliasedUnitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsed.Quantity,
                    UnitOfMeasureAbbreviation = aliasedUnitOfMeasure.Abbreviation,
                    UnitOfMeasureId = aliasedUnitOfMeasure.Id,
                    WasClaudeResolved = false
                };
            }
        }

        // Step 3: count fallback (ingredient noun, or a skipped size descriptor).
        if (parsed.IsCountFallbackEligible)
        {
            var eachUnitOfMeasure = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Abbreviation == "ea", cancellationToken);

            if (eachUnitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsed.Quantity,
                    UnitOfMeasureAbbreviation = eachUnitOfMeasure.Abbreviation,
                    UnitOfMeasureId = eachUnitOfMeasure.Id,
                    WasClaudeResolved = false
                };
            }
        }

        return null;
    }

    // Column caps from UnresolvedUnitOfMeasureTokenConfiguration. Kept in sync
    // here so the ingest path (which feeds noisy Kaggle measure strings into
    // this method) never trips a 22001 on SaveChanges.
    private const int UnitTokenMaxLength = 100;
    private const int SampleStringMaxLength = 500;

    /// <summary>
    /// Upserts an <see cref="UnresolvedUnitOfMeasureToken"/> row for the given unit token.
    /// First occurrence inserts; subsequent occurrences increment the count and
    /// refresh the sample context so the review UI shows the most recent usage.
    /// Inputs are truncated to their column caps before both lookup and insert,
    /// so the dedupe key stays consistent and the write never overflows.
    /// </summary>
    private async Task UpsertUnresolvedTokenAsync(
        string unitToken,
        string measureString,
        string ingredientName,
        CancellationToken cancellationToken)
    {
        // If there is no unit token to queue (empty measure string or pure-numeric
        // string with no remainder), there is nothing actionable for the user to
        // review -- skip the queue write and let the caller handle the empty case.
        if (string.IsNullOrWhiteSpace(unitToken))
        {
            return;
        }

        var safeUnitToken = Truncate(unitToken, UnitTokenMaxLength);
        var safeMeasureString = Truncate(measureString, SampleStringMaxLength);
        var safeIngredientName = Truncate(ingredientName, SampleStringMaxLength);

        var now = DateTime.UtcNow;

        var existing = await dbContext.UnresolvedUnitOfMeasureTokens
            .FirstOrDefaultAsync(t => t.UnitToken == safeUnitToken, cancellationToken);

        if (existing is not null)
        {
            existing.Count += 1;
            existing.LastSeenAt = now;
            existing.SampleMeasureString = safeMeasureString;
            existing.SampleIngredientContext = safeIngredientName;
        }
        else
        {
            dbContext.UnresolvedUnitOfMeasureTokens.Add(new UnresolvedUnitOfMeasureToken
            {
                Count = 1,
                FirstSeenAt = now,
                Id = Guid.NewGuid(),
                LastSeenAt = now,
                SampleIngredientContext = safeIngredientName,
                SampleMeasureString = safeMeasureString,
                UnitToken = safeUnitToken
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
