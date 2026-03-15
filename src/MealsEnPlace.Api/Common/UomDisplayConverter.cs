using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// Converts quantities stored in metric base units (ml, g, ea) to display units
/// determined by the user's <see cref="DisplaySystem"/> preference.
/// <para>
/// This converter runs at the API response layer, after all service computation,
/// before response serialization. Services always work in base units — never apply
/// display conversion inside a service.
/// </para>
/// <para>
/// Imperial display mappings:
/// <list type="bullet">
///   <item><description>ml → fl oz (below 59 ml), cups (59–946 ml), quarts (above 946 ml)</description></item>
///   <item><description>g → oz (below 454 g), lb (454 g and above)</description></item>
///   <item><description>ea → ea (no conversion)</description></item>
/// </list>
/// Metric display: pass base units through unchanged.
/// </para>
/// </summary>
public class UomDisplayConverter(MealsEnPlaceDbContext dbContext)
{
    private DisplaySystem? _cachedDisplaySystem;

    /// <summary>
    /// Converts <paramref name="baseQuantity"/> from the base unit implied by
    /// <paramref name="uomType"/> to the display unit appropriate for the current
    /// <see cref="DisplaySystem"/> preference.
    /// </summary>
    /// <param name="baseQuantity">Quantity in the metric base unit (ml, g, or ea).</param>
    /// <param name="uomType">Dimensional type of the quantity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple of the converted quantity and its display abbreviation.
    /// </returns>
    public async Task<(decimal DisplayQuantity, string DisplayAbbreviation)> ConvertAsync(
        decimal baseQuantity,
        UomType uomType,
        CancellationToken cancellationToken = default)
    {
        var displaySystem = await GetDisplaySystemAsync(cancellationToken);

        if (displaySystem == DisplaySystem.Metric || uomType == UomType.Arbitrary || uomType == UomType.Count)
        {
            var abbreviation = uomType switch
            {
                UomType.Volume => "ml",
                UomType.Weight => "g",
                _              => "ea"
            };
            return (baseQuantity, abbreviation);
        }

        // Imperial conversions
        return uomType switch
        {
            UomType.Volume => ConvertVolumeToImperial(baseQuantity),
            UomType.Weight => ConvertWeightToImperial(baseQuantity),
            _              => (baseQuantity, "ea")
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static (decimal Quantity, string Abbreviation) ConvertVolumeToImperial(decimal ml)
    {
        return ml switch
        {
            < 59m    => (Math.Round(ml / 29.574m, 2), "fl oz"),
            <= 946m  => (Math.Round(ml / 236.588m, 2), "cups"),
            _        => (Math.Round(ml / 946.353m, 2), "qt")
        };
    }

    private static (decimal Quantity, string Abbreviation) ConvertWeightToImperial(decimal g)
    {
        return g switch
        {
            < 454m => (Math.Round(g / 28.350m, 2), "oz"),
            _      => (Math.Round(g / 453.592m, 2), "lb")
        };
    }

    private async Task<DisplaySystem> GetDisplaySystemAsync(CancellationToken cancellationToken)
    {
        if (_cachedDisplaySystem.HasValue)
        {
            return _cachedDisplaySystem.Value;
        }

        var prefs = await dbContext.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        _cachedDisplaySystem = prefs?.DisplaySystem ?? DisplaySystem.Imperial;
        return _cachedDisplaySystem.Value;
    }
}
