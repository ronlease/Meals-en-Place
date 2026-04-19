using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// The result of a unit of measure conversion operation.
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
    public string FromUnitOfMeasure { get; init; } = string.Empty;

    /// <summary>True when the conversion completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>The abbreviation of the target unit (e.g., "ml").</summary>
    public string ToUnitOfMeasure { get; init; } = string.Empty;

    /// <summary>Creates a successful conversion result.</summary>
    public static ConversionResult Ok(decimal convertedQuantity, string fromUnitOfMeasure, string toUnitOfMeasure) =>
        new() { ConvertedQuantity = convertedQuantity, FromUnitOfMeasure = fromUnitOfMeasure, Success = true, ToUnitOfMeasure = toUnitOfMeasure };

    /// <summary>Creates a failure result when a unit of measure ID is not found in the database.</summary>
    public static ConversionResult NotFound(Guid unitOfMeasureId) =>
        new()
        {
            ErrorMessage = $"Unit of measure with ID '{unitOfMeasureId}' was not found.",
            Success = false
        };
}

/// <summary>
/// Provides deterministic unit of measure conversion between canonical units using pre-seeded conversion factors.
/// All computation is performed in metric base units (ml for Volume, g for Weight, ea for Count).
/// Cross-type conversions are never attempted — a failure result is returned instead.
/// </summary>
public interface IUnitOfMeasureConversionService
{
    /// <summary>
    /// Converts a quantity in the given unit to the metric base unit for its <see cref="UnitOfMeasureType"/>
    /// (ml for Volume, g for Weight, ea for Count).
    /// If the unit is already the base unit, the quantity is returned unchanged.
    /// </summary>
    /// <param name="quantity">The numeric quantity in <paramref name="unitOfMeasureId"/> units.</param>
    /// <param name="unitOfMeasureId">The <see cref="UnitOfMeasure.Id"/> of the source unit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The quantity expressed in the base unit for the unit of measure's type,
    /// or a failure <see cref="ConversionResult"/> if the unit of measure is not found.
    /// </returns>
    Task<ConversionResult> ConvertToBaseUnitsAsync(
        decimal quantity,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// <inheritdoc cref="IUnitOfMeasureConversionService"/>
/// </summary>
public class UnitOfMeasureConversionService(MealsEnPlaceDbContext dbContext) : IUnitOfMeasureConversionService
{
    /// <inheritdoc />
    public async Task<ConversionResult> ConvertToBaseUnitsAsync(
        decimal quantity,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        var unitOfMeasure = await FindUnitOfMeasureAsync(unitOfMeasureId, cancellationToken);
        if (unitOfMeasure is null)
        {
            return ConversionResult.NotFound(unitOfMeasureId);
        }

        // Base units have ConversionFactor = 1.0 and BaseUnitOfMeasureId = null.
        var baseQuantity = quantity * unitOfMeasure.ConversionFactor;

        var baseAbbreviation = unitOfMeasure.UnitOfMeasureType switch
        {
            UnitOfMeasureType.Volume => "ml",
            UnitOfMeasureType.Weight => "g",
            UnitOfMeasureType.Count => "ea",
            _ => unitOfMeasure.Abbreviation
        };

        return ConversionResult.Ok(baseQuantity, unitOfMeasure.Abbreviation, baseAbbreviation);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Task<UnitOfMeasure?> FindUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UnitsOfMeasure
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}
