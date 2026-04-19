using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// The result of a <see cref="IUomNormalizationService.NormalizeAsync"/> call.
/// Describes the resolved quantity, unit, confidence, and whether Claude was invoked.
/// </summary>
public sealed class NormalizationResult
{
    /// <summary>
    /// Confidence level of the resolution.
    /// For deterministic lookups this is always <see cref="ClaudeConfidence.High"/>.
    /// For Claude resolutions this reflects <see cref="UomResolutionResult.Confidence"/>.
    /// </summary>
    public ClaudeConfidence Confidence { get; init; }

    /// <summary>
    /// Optional notes from a Claude resolution explaining assumptions or flagging uncertainty.
    /// Null for deterministic resolutions.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>The resolved numeric quantity in <see cref="UomAbbreviation"/> units.</summary>
    public decimal Quantity { get; init; }

    /// <summary>The abbreviation of the resolved canonical unit (e.g., "g", "ml", "ea", "cup").</summary>
    public string UomAbbreviation { get; init; } = string.Empty;

    /// <summary>
    /// The <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Id"/> of the resolved unit.
    /// <see cref="Guid.Empty"/> when Claude could not resolve to a known unit
    /// or when the ingredient was deferred to the review queue.
    /// </summary>
    public Guid UomId { get; init; }

    /// <summary>
    /// True when Claude was invoked to resolve the measure string.
    /// False when the resolution was performed deterministically or the
    /// ingredient was deferred to the review queue.
    /// </summary>
    public bool WasClaudeResolved { get; init; }

    /// <summary>
    /// True when the ingredient was deferred to the
    /// <see cref="MealsEnPlace.Api.Models.Entities.UnresolvedUomToken"/> review
    /// queue because ingest mode was set and deterministic resolution failed.
    /// The caller should persist the ingredient in an unresolved state until
    /// the user decides how to map the token.
    /// </summary>
    public bool WasDeferredToQueue { get; init; }
}

/// <summary>
/// Normalizes raw measure strings from recipe imports and inventory entries into
/// canonical units tracked in the UOM conversion table.
/// <para>
/// Resolution order:
/// <list type="number">
///   <item><description>Parse a numeric quantity and unit token from the measure string.</description></item>
///   <item><description>Look up the unit token against <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Abbreviation"/> or <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Name"/> (case-insensitive).</description></item>
///   <item><description>If not found, look up against <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasureAlias"/> to catch dataset variants (e.g., "c.", "Tbsp.", "lbs").</description></item>
///   <item><description>If still not found and a positive quantity parsed, default to "each" (count-with-ingredient-noun pattern, e.g. "4 chicken breasts").</description></item>
///   <item><description>If still not found, fall back to Claude via <see cref="IClaudeService.ResolveUomAsync"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// Container references must be filtered out by <see cref="ContainerReferenceDetector"/> before
/// calling this service — this service does not repeat that check.
/// </para>
/// </summary>
public interface IUomNormalizationService
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
    /// <see cref="MealsEnPlace.Api.Models.Entities.UnresolvedUomToken"/> row to
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
/// <inheritdoc cref="IUomNormalizationService"/>
/// </summary>
public class UomNormalizationService(
    IClaudeService claudeService,
    MealsEnPlaceDbContext dbContext) : IUomNormalizationService
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

        // Claude fallback for colloquial or unmapped units.
        var claudeResult = await claudeService.ResolveUomAsync(measureString, ingredientName);

        // Attempt to map the Claude-resolved abbreviation back to a known UOM.
        var resolvedUom = string.IsNullOrWhiteSpace(claudeResult.ResolvedUom)
            ? null
            : await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Abbreviation.ToLower() == claudeResult.ResolvedUom.ToLower(),
                    cancellationToken);

        return new NormalizationResult
        {
            Confidence = claudeResult.Confidence,
            Notes = claudeResult.Notes,
            Quantity = claudeResult.ResolvedQuantity,
            UomAbbreviation = resolvedUom?.Abbreviation ?? claudeResult.ResolvedUom,
            UomId = resolvedUom?.Id ?? Guid.Empty,
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

        // No deterministic match -- queue for review instead of invoking Claude.
        await UpsertUnresolvedTokenAsync(
            unitToken, measureString, ingredientName, cancellationToken);

        return new NormalizationResult
        {
            Confidence = ClaudeConfidence.Low,
            Notes = "Deferred to review queue.",
            Quantity = parsedQuantity,
            UomAbbreviation = string.Empty,
            UomId = Guid.Empty,
            WasClaudeResolved = false,
            WasDeferredToQueue = true
        };
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
            var uom = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Abbreviation.ToLower() == unitToken.ToLower()
                         || u.Abbreviation.ToLower() == normalizedToken
                         || u.Name.ToLower() == unitToken.ToLower()
                         || u.Name.ToLower() == normalizedToken,
                    cancellationToken);

            if (uom is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsedQuantity,
                    UomAbbreviation = uom.Abbreviation,
                    UomId = uom.Id,
                    WasClaudeResolved = false
                };
            }

            // Step 2: alias-table lookup for dataset variants (e.g. "c.", "Tbsp.", "lbs").
            var aliasedUom = await dbContext.UnitOfMeasureAliases
                .AsNoTracking()
                .Where(a => a.Alias.ToLower() == unitToken.ToLower())
                .Select(a => a.UnitOfMeasure!)
                .FirstOrDefaultAsync(cancellationToken);

            if (aliasedUom is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsedQuantity,
                    UomAbbreviation = aliasedUom.Abbreviation,
                    UomId = aliasedUom.Id,
                    WasClaudeResolved = false
                };
            }
        }

        // Step 3: count-with-ingredient-noun fallback.
        if (parsedQuantity > 0m && !string.IsNullOrWhiteSpace(unitToken))
        {
            var eachUom = await dbContext.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Abbreviation == "ea", cancellationToken);

            if (eachUom is not null)
            {
                return new NormalizationResult
                {
                    Confidence = ClaudeConfidence.High,
                    Quantity = parsedQuantity,
                    UomAbbreviation = eachUom.Abbreviation,
                    UomId = eachUom.Id,
                    WasClaudeResolved = false
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Upserts an <see cref="UnresolvedUomToken"/> row for the given unit token.
    /// First occurrence inserts; subsequent occurrences increment the count and
    /// refresh the sample context so the review UI shows the most recent usage.
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

        var now = DateTime.UtcNow;

        var existing = await dbContext.UnresolvedUomTokens
            .FirstOrDefaultAsync(t => t.UnitToken == unitToken, cancellationToken);

        if (existing is not null)
        {
            existing.Count += 1;
            existing.LastSeenAt = now;
            existing.SampleMeasureString = measureString;
            existing.SampleIngredientContext = ingredientName;
        }
        else
        {
            dbContext.UnresolvedUomTokens.Add(new UnresolvedUomToken
            {
                Count = 1,
                FirstSeenAt = now,
                Id = Guid.NewGuid(),
                LastSeenAt = now,
                SampleIngredientContext = ingredientName,
                SampleMeasureString = measureString,
                UnitToken = unitToken
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Splits a raw measure string into a numeric quantity and a unit token.
    /// Handles common formats: "2 cups", "500g", "1.5 tbsp", "a knob".
    /// Returns (0, remainingText) when no leading number is found so Claude can handle it.
    /// </summary>
    private static (decimal Quantity, string UnitToken) ParseMeasureString(string measureString)
    {
        var trimmed = measureString.Trim();

        // Find the end of a leading numeric portion (digits, dot, slash for fractions).
        var numericEnd = 0;
        while (numericEnd < trimmed.Length
               && (char.IsDigit(trimmed[numericEnd])
                   || trimmed[numericEnd] == '.'
                   || trimmed[numericEnd] == '/'))
        {
            numericEnd++;
        }

        if (numericEnd == 0)
        {
            // No leading number — pass the whole string as the unit token for Claude.
            return (0m, trimmed);
        }

        var numericPart = trimmed[..numericEnd];
        var remainder = trimmed[numericEnd..].Trim();

        var quantity = ParseFractionOrDecimal(numericPart);
        return (quantity, remainder);
    }

    /// <summary>
    /// Parses a numeric string that may be a simple decimal ("1.5") or a fraction ("1/2").
    /// Returns 0 on parse failure.
    /// </summary>
    private static decimal ParseFractionOrDecimal(string numericPart)
    {
        if (numericPart.Contains('/'))
        {
            var parts = numericPart.Split('/');
            if (parts.Length == 2
                && decimal.TryParse(parts[0], out var numerator)
                && decimal.TryParse(parts[1], out var denominator)
                && denominator != 0)
            {
                return numerator / denominator;
            }

            return 0m;
        }

        return decimal.TryParse(numericPart, out var value) ? value : 0m;
    }
}
