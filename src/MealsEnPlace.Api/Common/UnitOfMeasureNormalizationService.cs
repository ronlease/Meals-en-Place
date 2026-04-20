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
///   <item><description>Parse a numeric quantity and unit token from the measure string.</description></item>
///   <item><description>Look up the unit token against <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Abbreviation"/> or <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Name"/> (case-insensitive).</description></item>
///   <item><description>If not found, look up against <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasureAlias"/> to catch dataset variants (e.g., "c.", "Tbsp.", "lbs").</description></item>
///   <item><description>If still not found and a positive quantity parsed, default to "each" (count-with-ingredient-noun pattern, e.g. "4 chicken breasts").</description></item>
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
    /// as <see cref="NormalizeAsync"/> (abbreviation / name / alias / count-noun
    /// fallback), but when no deterministic match is found it writes an
    /// <see cref="MealsEnPlace.Api.Models.Entities.UnresolvedUnitOfMeasureToken"/> row to
    /// the review queue instead of invoking Claude. This preserves Claude quota
    /// during bulk ingest and lets the user decide how to map recurring tokens
    /// in one place.
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="NormalizationResult"/> on a deterministic match; null
    /// otherwise.
    /// </returns>
    Task<NormalizationResult?> TryResolveDeterministicallyAsync(
        string measureString,
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
        var (parsedQuantity, unitToken) = ParseMeasureString(measureString);

        var deterministic = await TryResolveDeterministicallyCoreAsync(
            parsedQuantity, unitToken, cancellationToken);

        if (deterministic is not null)
        {
            return deterministic;
        }

        // MEP-032: when no Claude API key is configured, route the unresolved
        // token to the review queue instead of calling the Claude fallback.
        if (!await claudeAvailability.IsConfiguredAsync(cancellationToken))
        {
            return await DeferToReviewQueueAsync(
                parsedQuantity, unitToken, measureString, ingredientName, cancellationToken);
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
        var (parsedQuantity, unitToken) = ParseMeasureString(measureString);

        var deterministic = await TryResolveDeterministicallyCoreAsync(
            parsedQuantity, unitToken, cancellationToken);

        if (deterministic is not null)
        {
            return deterministic;
        }

        return await DeferToReviewQueueAsync(
            parsedQuantity, unitToken, measureString, ingredientName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NormalizationResult?> TryResolveDeterministicallyAsync(
        string measureString,
        CancellationToken cancellationToken = default)
    {
        var (parsedQuantity, unitToken) = ParseMeasureString(measureString);
        return await TryResolveDeterministicallyCoreAsync(
            parsedQuantity, unitToken, cancellationToken);
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
        decimal parsedQuantity,
        string unitToken,
        CancellationToken cancellationToken)
    {
        // Step 1: deterministic lookup by abbreviation / name (with plural stripping).
        if (!string.IsNullOrWhiteSpace(unitToken))
        {
            var normalizedToken = unitToken.ToLower().TrimEnd('s');
            var unitOfMeasure = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Abbreviation.ToLower() == unitToken.ToLower()
                         || u.Abbreviation.ToLower() == normalizedToken
                         || u.Name.ToLower() == unitToken.ToLower()
                         || u.Name.ToLower() == normalizedToken,
                    cancellationToken);

            if (unitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsedQuantity,
                    UnitOfMeasureAbbreviation = unitOfMeasure.Abbreviation,
                    UnitOfMeasureId = unitOfMeasure.Id,
                    WasClaudeResolved = false
                };
            }

            // Step 2: alias-table lookup for dataset variants (e.g. "c.", "Tbsp.", "lbs").
            var aliasedUnitOfMeasure = await dbContext.UnitOfMeasureAliases
                .AsNoTracking()
                .Where(a => a.Alias.ToLower() == unitToken.ToLower())
                .Select(a => a.UnitOfMeasure!)
                .FirstOrDefaultAsync(cancellationToken);

            if (aliasedUnitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsedQuantity,
                    UnitOfMeasureAbbreviation = aliasedUnitOfMeasure.Abbreviation,
                    UnitOfMeasureId = aliasedUnitOfMeasure.Id,
                    WasClaudeResolved = false
                };
            }
        }

        // Step 3: count-with-ingredient-noun fallback.
        if (parsedQuantity > 0m && !string.IsNullOrWhiteSpace(unitToken))
        {
            var eachUnitOfMeasure = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Abbreviation == "ea", cancellationToken);

            if (eachUnitOfMeasure is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsedQuantity,
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

    /// <summary>
    /// Back-compat delegate to <see cref="UnitOfMeasureTokenParser.Parse"/> so existing
    /// call sites in this class do not need refactoring.
    /// </summary>
    private static (decimal Quantity, string UnitToken) ParseMeasureString(string measureString) =>
        UnitOfMeasureTokenParser.Parse(measureString);
}
