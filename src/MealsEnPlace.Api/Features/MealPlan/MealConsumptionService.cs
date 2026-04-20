using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <inheritdoc cref="IMealConsumptionService"/>
public sealed class MealConsumptionService(
    MealsEnPlaceDbContext dbContext,
    IUnitOfMeasureConversionService unitOfMeasureConversionService) : IMealConsumptionService
{
    public async Task<ConsumeMealResult?> ConsumeAsync(Guid slotId, CancellationToken cancellationToken = default)
    {
        var slot = await LoadSlotWithRecipeAsync(slotId, cancellationToken);
        if (slot is null)
        {
            return null;
        }

        var preference = await ReadAutoDepleteOnConsumeAsync(cancellationToken);
        var now = DateTime.UtcNow;
        slot.ConsumedAt = now;
        slot.ConsumedWithAutoDeplete = preference;

        var shortIngredients = new List<ShortIngredient>();

        if (preference)
        {
            shortIngredients.AddRange(await DepleteInventoryAsync(slot, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ConsumeMealResult
        {
            AutoDepleteApplied = preference,
            ConsumedAt = now,
            ShortIngredients = shortIngredients
        };
    }

    public async Task<bool> UnconsumeAsync(Guid slotId, CancellationToken cancellationToken = default)
    {
        var slot = await dbContext.MealPlanSlots
            .Include(s => s.ConsumeAuditEntries)
            .FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken);
        if (slot is null)
        {
            return false;
        }

        if (slot.ConsumedAt is null)
        {
            // Nothing to undo. Treated as idempotent success.
            return true;
        }

        // State-only reverse when auto-deplete was off at consume time.
        if (slot.ConsumedWithAutoDeplete == true)
        {
            await RestoreInventoryAsync(slot, cancellationToken);
        }

        slot.ConsumedAt = null;
        slot.ConsumedWithAutoDeplete = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<List<ShortIngredient>> DepleteInventoryAsync(
        MealPlanSlot slot, CancellationToken cancellationToken)
    {
        var shortIngredients = new List<ShortIngredient>();

        foreach (var recipeIngredient in slot.Recipe.RecipeIngredients.Where(ri => ri.UnitOfMeasureId.HasValue))
        {
            var required = recipeIngredient.Quantity;
            var recipeUnitOfMeasureId = recipeIngredient.UnitOfMeasureId!.Value;
            var recipeUnitConversion = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(
                required, recipeUnitOfMeasureId, cancellationToken);
            if (!recipeUnitConversion.Success)
            {
                continue;
            }

            var requiredBase = recipeUnitConversion.ConvertedQuantity;
            var remainingBase = requiredBase;

            var candidateRows = await dbContext.InventoryItems
                .Where(i => i.CanonicalIngredientId == recipeIngredient.CanonicalIngredientId)
                .Include(i => i.UnitOfMeasure)
                .ToListAsync(cancellationToken);

            // Oldest-expiry first; null expiry goes last (least-perishable = deplete last).
            candidateRows = candidateRows
                .OrderBy(i => i.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(i => i.ExpiryDate ?? DateOnly.MaxValue)
                .ToList();

            foreach (var row in candidateRows)
            {
                if (remainingBase <= 0m)
                {
                    break;
                }

                var rowConversion = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(
                    row.Quantity, row.UnitOfMeasureId, cancellationToken);
                if (!rowConversion.Success || rowConversion.ConvertedQuantity <= 0m)
                {
                    continue;
                }

                // Cross-type inventory / recipe entries (Volume vs. Weight) cannot
                // be aggregated without ingredient-specific density data, so skip
                // mismatched rows rather than guessing.
                if (row.UnitOfMeasure.UnitOfMeasureType != recipeIngredient.UnitOfMeasure!.UnitOfMeasureType)
                {
                    continue;
                }

                var takeBase = Math.Min(rowConversion.ConvertedQuantity, remainingBase);
                var rowBasePerOne = rowConversion.ConvertedQuantity / row.Quantity;
                if (rowBasePerOne == 0m)
                {
                    continue;
                }
                var takeInRowUnits = takeBase / rowBasePerOne;
                // Guard against rounding leaving a sub-unit residue.
                if (takeInRowUnits > row.Quantity)
                {
                    takeInRowUnits = row.Quantity;
                }

                row.Quantity -= takeInRowUnits;
                remainingBase -= takeBase;

                dbContext.ConsumeAuditEntries.Add(new ConsumeAuditEntry
                {
                    CanonicalIngredientId = recipeIngredient.CanonicalIngredientId,
                    CreatedAt = DateTime.UtcNow,
                    DeductedQuantity = takeInRowUnits,
                    Id = Guid.NewGuid(),
                    MealPlanSlotId = slot.Id,
                    OriginalExpiryDate = row.ExpiryDate,
                    OriginalInventoryItemId = row.Id,
                    OriginalLocation = row.Location,
                    UnitOfMeasureId = row.UnitOfMeasureId
                });
            }

            if (remainingBase > 0m)
            {
                // Report the shortage in the recipe's original unit so the UI
                // matches what the user sees in the recipe view.
                var shortInRecipeUnits = remainingBase / (requiredBase / required);
                shortIngredients.Add(new ShortIngredient
                {
                    IngredientName = recipeIngredient.CanonicalIngredient.Name,
                    ShortBy = shortInRecipeUnits,
                    UnitOfMeasureAbbreviation = recipeIngredient.UnitOfMeasure?.Abbreviation ?? string.Empty
                });
            }
        }

        return shortIngredients;
    }

    private async Task<MealPlanSlot?> LoadSlotWithRecipeAsync(
        Guid slotId, CancellationToken cancellationToken)
    {
        return await dbContext.MealPlanSlots
            .Include(s => s.Recipe)
                .ThenInclude(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.CanonicalIngredient)
            .Include(s => s.Recipe)
                .ThenInclude(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.UnitOfMeasure)
            .FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken);
    }

    private async Task<bool> ReadAutoDepleteOnConsumeAsync(CancellationToken cancellationToken)
    {
        var prefs = await dbContext.UserPreferences.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return prefs?.AutoDepleteOnConsume ?? false;
    }

    private async Task RestoreInventoryAsync(MealPlanSlot slot, CancellationToken cancellationToken)
    {
        foreach (var audit in slot.ConsumeAuditEntries)
        {
            InventoryItem? target = null;
            if (audit.OriginalInventoryItemId is Guid originalId)
            {
                target = await dbContext.InventoryItems
                    .FirstOrDefaultAsync(i => i.Id == originalId, cancellationToken);
            }

            if (target is not null)
            {
                target.Quantity += audit.DeductedQuantity;
            }
            else
            {
                dbContext.InventoryItems.Add(new InventoryItem
                {
                    CanonicalIngredientId = audit.CanonicalIngredientId,
                    ExpiryDate = audit.OriginalExpiryDate,
                    Id = Guid.NewGuid(),
                    Location = audit.OriginalLocation,
                    Notes = null,
                    Quantity = audit.DeductedQuantity,
                    UnitOfMeasureId = audit.UnitOfMeasureId
                });
            }
        }

        // Audit rows are consumed by the restore; cascade delete via the
        // slot-owned collection keeps the database tidy.
        dbContext.ConsumeAuditEntries.RemoveRange(slot.ConsumeAuditEntries);
    }
}
