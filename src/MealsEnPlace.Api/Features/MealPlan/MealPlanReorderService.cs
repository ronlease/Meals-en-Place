using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <inheritdoc cref="IMealPlanReorderService"/>
public sealed class MealPlanReorderService(
    MealsEnPlaceDbContext dbContext,
    IMealPlanService mealPlanService) : IMealPlanReorderService
{
    public async Task<MealPlanResponse?> ApplyAsync(
        Guid mealPlanId,
        int urgencyWindowDays,
        CancellationToken cancellationToken = default)
    {
        var plan = await LoadPlanAsync(mealPlanId, tracked: true, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiringInventory = await LoadExpiringInventoryAsync(today, urgencyWindowDays, cancellationToken);
        var reorder = ComputeReorder(plan, expiringInventory, today, urgencyWindowDays);

        if (reorder.HasChanges)
        {
            var slotsById = plan.Slots.ToDictionary(s => s.Id);
            foreach (var change in reorder.Changes)
            {
                slotsById[change.Id].DayOfWeek = change.ProposedDay;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await mealPlanService.GetMealPlanAsync(mealPlanId, cancellationToken);
    }

    public async Task<ReorderPreviewResponse?> PreviewAsync(
        Guid mealPlanId,
        int urgencyWindowDays,
        CancellationToken cancellationToken = default)
    {
        var plan = await LoadPlanAsync(mealPlanId, tracked: false, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiringInventory = await LoadExpiringInventoryAsync(today, urgencyWindowDays, cancellationToken);
        return ComputeReorder(plan, expiringInventory, today, urgencyWindowDays);
    }

    /// <summary>
    /// Pure function: given a loaded plan and the current expiring-inventory
    /// snapshot, produce the reorder. Extracted so it's testable without EF.
    /// </summary>
    private static ReorderPreviewResponse ComputeReorder(
        Models.Entities.MealPlan plan,
        Dictionary<Guid, List<DateOnly>> expiringInventoryByIngredient,
        DateOnly today,
        int urgencyWindowDays)
    {
        if (urgencyWindowDays <= 0)
        {
            urgencyWindowDays = 7;
        }

        var scoredSlots = plan.Slots
            .Select(slot => new
            {
                Slot = slot,
                Score = ScoreSlot(slot, expiringInventoryByIngredient, today, urgencyWindowDays)
            })
            .ToList();

        if (scoredSlots.All(s => s.Score == 0m))
        {
            return new ReorderPreviewResponse
            {
                Changes = [],
                HasChanges = false,
                Reason = "No planned recipes use ingredients expiring within the next "
                    + $"{urgencyWindowDays} day(s). Nothing to reorder.",
                UrgencyWindowDays = urgencyWindowDays
            };
        }

        // Reorder within each MealSlot: the recipes stay in their meal occasion
        // (Breakfast stays Breakfast) but their DayOfWeek permutes so higher-
        // urgency recipes take earlier days. Ties keep their original relative
        // order via a stable sort.
        var changes = new List<ReorderedSlotDto>();

        foreach (var mealSlotGroup in scoredSlots.GroupBy(s => s.Slot.MealSlot))
        {
            var originalDays = mealSlotGroup
                .Select(s => s.Slot.DayOfWeek)
                .OrderBy(DayOrderIndex)
                .ToList();

            var ranked = mealSlotGroup
                .OrderByDescending(s => s.Score)
                .ThenBy(s => DayOrderIndex(s.Slot.DayOfWeek))
                .ToList();

            for (var i = 0; i < ranked.Count; i++)
            {
                var slot = ranked[i].Slot;
                var proposedDay = originalDays[i];
                if (slot.DayOfWeek != proposedDay)
                {
                    changes.Add(new ReorderedSlotDto
                    {
                        Id = slot.Id,
                        MealSlot = slot.MealSlot,
                        OriginalDay = slot.DayOfWeek,
                        ProposedDay = proposedDay,
                        RecipeId = slot.RecipeId,
                        RecipeTitle = slot.Recipe?.Title ?? string.Empty,
                        UrgencyScore = ranked[i].Score
                    });
                }
            }
        }

        if (changes.Count == 0)
        {
            return new ReorderPreviewResponse
            {
                Changes = [],
                HasChanges = false,
                Reason = "The current order already prioritizes recipes with expiring ingredients.",
                UrgencyWindowDays = urgencyWindowDays
            };
        }

        return new ReorderPreviewResponse
        {
            Changes = changes
                .OrderBy(c => c.MealSlot)
                .ThenBy(c => DayOrderIndex(c.ProposedDay))
                .ToList(),
            HasChanges = true,
            Reason = null,
            UrgencyWindowDays = urgencyWindowDays
        };
    }

    /// <summary>
    /// Monday-first ordering. The existing plan-generation code uses the same
    /// rotation elsewhere (see <c>MealPlanService</c>), so this stays
    /// consistent.
    /// </summary>
    private static int DayOrderIndex(DayOfWeek day) => ((int)day + 6) % 7;

    private async Task<Dictionary<Guid, List<DateOnly>>> LoadExpiringInventoryAsync(
        DateOnly today, int urgencyWindowDays, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(urgencyWindowDays);
        var rows = await dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= cutoff)
            .Select(i => new { i.CanonicalIngredientId, ExpiryDate = i.ExpiryDate!.Value })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.CanonicalIngredientId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.ExpiryDate).ToList());
    }

    private async Task<Models.Entities.MealPlan?> LoadPlanAsync(
        Guid mealPlanId, bool tracked, CancellationToken cancellationToken)
    {
        var query = dbContext.MealPlans
            .Include(mp => mp.Slots)
                .ThenInclude(s => s.Recipe)
                    .ThenInclude(r => r.RecipeIngredients);

        if (!tracked)
        {
            return await query.AsNoTracking().FirstOrDefaultAsync(mp => mp.Id == mealPlanId, cancellationToken);
        }

        return await query.FirstOrDefaultAsync(mp => mp.Id == mealPlanId, cancellationToken);
    }

    private static decimal ScoreSlot(
        MealPlanSlot slot,
        Dictionary<Guid, List<DateOnly>> expiringInventoryByIngredient,
        DateOnly today,
        int urgencyWindowDays)
    {
        if (slot.Recipe?.RecipeIngredients is null)
        {
            return 0m;
        }

        decimal score = 0m;
        foreach (var ri in slot.Recipe.RecipeIngredients)
        {
            if (!expiringInventoryByIngredient.TryGetValue(ri.CanonicalIngredientId, out var expiries))
            {
                continue;
            }

            // The earliest expiry among candidate rows drives urgency.
            var earliest = expiries.Min();
            var daysUntilExpiry = earliest.DayNumber - today.DayNumber;
            if (daysUntilExpiry < 0)
            {
                // Already past expiry — treat as maximum urgency rather than
                // negative (negative would cancel other ingredient urgency).
                score += 1m;
                continue;
            }
            if (daysUntilExpiry > urgencyWindowDays)
            {
                continue;
            }

            // Linear decay across the urgency window. Ingredients expiring
            // today score 1.0, ingredients at the edge of the window score 0.
            score += (decimal)(urgencyWindowDays - daysUntilExpiry) / urgencyWindowDays;
        }

        return score;
    }
}
