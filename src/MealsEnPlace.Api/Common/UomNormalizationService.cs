using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
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
    /// <see cref="Guid.Empty"/> when Claude could not resolve to a known unit.
    /// </summary>
    public Guid UomId { get; init; }

    /// <summary>
    /// True when Claude was invoked to resolve the measure string.
    /// False when the resolution was performed deterministically via the conversion table.
    /// </summary>
    public bool WasClaudeResolved { get; init; }
}

/// <summary>
/// Normalizes raw measure strings from recipe imports and inventory entries into
/// canonical units tracked in the UOM conversion table.
/// <para>
/// Resolution order:
/// <list type="number">
///   <item><description>Parse a numeric quantity and unit token from the measure string.</description></item>
///   <item><description>Look up the unit token in the database by <see cref="MealsEnPlace.Api.Models.Entities.UnitOfMeasure.Abbreviation"/> (case-insensitive).</description></item>
///   <item><description>If not found, fall back to Claude via <see cref="IClaudeService.ResolveUomAsync"/>.</description></item>
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

        // Step 1: deterministic lookup by abbreviation (with plural stripping).
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
        }

        // Step 2: Claude fallback for colloquial or unmapped units.
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

    // ── Private helpers ───────────────────────────────────────────────────────

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
