using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// The result of a <see cref="IUomConversionService.ConvertBetweenAsync"/> operation.
/// On failure, <see cref="ConvertedQuantity"/> is zero and <see cref="ErrorMessage"/> describes the reason.
/// </summary>
public sealed class ConversionResult
{
    /// <summary>
    /// The converted quantity in the target unit.
    /// Zero when <see cref="Success"/> is false.
    /// </summary>
    public decimal ConvertedQuantity { get; init; }

    /// <summary>
    /// Human-readable error message when <see cref="Success"/> is false.
    /// Null when the conversion succeeded.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The abbreviation of the source unit (e.g., "cup").</summary>
    public string FromUom { get; init; } = string.Empty;

    /// <summary>True when the conversion completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>The abbreviation of the target unit (e.g., "ml").</summary>
    public string ToUom { get; init; } = string.Empty;

    /// <summary>Creates a successful conversion result.</summary>
    public static ConversionResult Ok(decimal convertedQuantity, string fromUom, string toUom) =>
        new() { ConvertedQuantity = convertedQuantity, FromUom = fromUom, Success = true, ToUom = toUom };

    /// <summary>Creates a failure result representing an incompatible cross-type conversion.</summary>
    public static ConversionResult CrossTypeFailure(string fromUom, string toUom) =>
        new()
        {
            ErrorMessage = $"Cannot convert between incompatible unit types: '{fromUom}' and '{toUom}'. " +
                           "Cross-type conversions (e.g., Volume to Weight) require ingredient-specific " +
                           "density data and are not supported.",
            FromUom = fromUom,
            Success = false,
            ToUom = toUom
        };

    /// <summary>Creates a failure result when a UOM ID is not found in the database.</summary>
    public static ConversionResult NotFound(Guid uomId) =>
        new()
        {
            ErrorMessage = $"Unit of measure with ID '{uomId}' was not found.",
            Success = false
        };
}

/// <summary>
/// Provides deterministic UOM conversion between canonical units using pre-seeded conversion factors.
/// All computation is performed in metric base units (ml for Volume, g for Weight, ea for Count).
/// Cross-type conversions are never attempted — a failure result is returned instead.
/// </summary>
public interface IUomConversionService
{
    /// <summary>
    /// Converts a quantity between two canonical units via their shared base unit.
    /// Returns a <see cref="ConversionResult"/> indicating success or failure.
    /// Cross-type conversions (e.g., Volume to Weight) always return a failure result.
    /// </summary>
    /// <param name="quantity">The numeric quantity in <paramref name="fromUomId"/> units.</param>
    /// <param name="fromUomId">The <see cref="UnitOfMeasure.Id"/> of the source unit.</param>
    /// <param name="toUomId">The <see cref="UnitOfMeasure.Id"/> of the target unit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ConversionResult> ConvertBetweenAsync(
        decimal quantity,
        Guid fromUomId,
        Guid toUomId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a quantity in the given unit to the metric base unit for its <see cref="UomType"/>
    /// (ml for Volume, g for Weight, ea for Count).
    /// If the unit is already the base unit, the quantity is returned unchanged.
    /// </summary>
    /// <param name="quantity">The numeric quantity in <paramref name="uomId"/> units.</param>
    /// <param name="uomId">The <see cref="UnitOfMeasure.Id"/> of the source unit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The quantity expressed in the base unit for the UOM's type,
    /// or a failure <see cref="ConversionResult"/> if the UOM is not found.
    /// </returns>
    Task<ConversionResult> ConvertToBaseUnitsAsync(
        decimal quantity,
        Guid uomId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// <inheritdoc cref="IUomConversionService"/>
/// </summary>
public class UomConversionService(MealsEnPlaceDbContext dbContext) : IUomConversionService
{
    /// <inheritdoc />
    public async Task<ConversionResult> ConvertBetweenAsync(
        decimal quantity,
        Guid fromUomId,
        Guid toUomId,
        CancellationToken cancellationToken = default)
    {
        var fromUom = await FindUomAsync(fromUomId, cancellationToken);
        if (fromUom is null)
        {
            return ConversionResult.NotFound(fromUomId);
        }

        var toUom = await FindUomAsync(toUomId, cancellationToken);
        if (toUom is null)
        {
            return ConversionResult.NotFound(toUomId);
        }

        if (fromUom.UomType != toUom.UomType)
        {
            return ConversionResult.CrossTypeFailure(fromUom.Abbreviation, toUom.Abbreviation);
        }

        // Convert from source → base → target
        var baseQuantity = quantity * fromUom.ConversionFactor;
        var convertedQuantity = baseQuantity / toUom.ConversionFactor;

        return ConversionResult.Ok(convertedQuantity, fromUom.Abbreviation, toUom.Abbreviation);
    }

    /// <inheritdoc />
    public async Task<ConversionResult> ConvertToBaseUnitsAsync(
        decimal quantity,
        Guid uomId,
        CancellationToken cancellationToken = default)
    {
        var uom = await FindUomAsync(uomId, cancellationToken);
        if (uom is null)
        {
            return ConversionResult.NotFound(uomId);
        }

        // Base units have ConversionFactor = 1.0 and BaseUomId = null.
        var baseQuantity = quantity * uom.ConversionFactor;

        var baseAbbreviation = uom.UomType switch
        {
            UomType.Volume => "ml",
            UomType.Weight => "g",
            UomType.Count => "ea",
            _ => uom.Abbreviation
        };

        return ConversionResult.Ok(baseQuantity, uom.Abbreviation, baseAbbreviation);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<UnitOfMeasure?> FindUomAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UnitsOfMeasure
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}
