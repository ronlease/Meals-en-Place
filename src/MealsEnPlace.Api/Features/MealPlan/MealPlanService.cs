using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Generates weekly meal plans by scoring recipes against current inventory,
/// waste-reduction priority, seasonal affinity, and variety constraints.
/// </summary>
public class MealPlanService(
    IClaudeAvailability claudeAvailability,
    IClaudeService claudeService,
    MealsEnPlaceDbContext dbContext,
    IUnitOfMeasureConversionService unitOfMeasureConversionService) : IMealPlanService
{
    private const int ExpiryBonusDays = 3;
    private const decimal ExpiryBonusPerIngredient = 0.15m;
    private const decimal SeasonalAffinityBonus = 0.1m;

    /// <inheritdoc />
    public async Task<MealPlanResponse> GenerateMealPlanAsync(GenerateMealPlanRequest request, CancellationToken cancellationToken = default)
    {
        var weekStart = request.WeekStartDate ?? GetCurrentWeekMonday();
        var name = InputSanitizer.SanitizeForStorage(request.Name, 200) ?? $"Meal Plan - {weekStart:yyyy-MM-dd}";
        var slotPreferences = request.SlotPreferences ?? BuildDefaultSlotPreferences();

        // Load inventory in base units
        var inventory = await InventoryBaseHelper.LoadInventoryAsync(dbContext, cancellationToken);
        var inventoryBase = await InventoryBaseHelper.ConvertToBaseUnitsAsync(inventory, unitOfMeasureConversionService, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Load candidate recipes
        var candidates = await LoadCandidateRecipesAsync(request, cancellationToken);

        // Score each recipe
        var scoredRecipes = new List<ScoredRecipe>();
        foreach (var recipe in candidates)
        {
            var score = ScoreRecipe(recipe, inventoryBase, today);
            scoredRecipes.Add(score);
        }

        // Sort by score descending
        scoredRecipes = scoredRecipes.OrderByDescending(s => s.FinalScore).ToList();

        // Check recent meal plans to avoid repetition within 7 days
        var recentRecipeIds = await GetRecentRecipeIdsAsync(weekStart, cancellationToken);

        // Greedy slot assignment
        var assignedRecipeIds = new HashSet<Guid>();
        var slots = new List<MealPlanSlot>();

        // Process days in chronological order to prioritize early slots for expiring items
        var orderedDays = slotPreferences
            .OrderBy(kvp => ((int)kvp.Key + 6) % 7) // Monday=0, Tuesday=1, ...
            .ToList();

        foreach (var (day, mealSlots) in orderedDays)
        {
            foreach (var mealSlot in mealSlots.OrderBy(s => s))
            {
                var bestRecipe = scoredRecipes
                    .FirstOrDefault(s =>
                        !assignedRecipeIds.Contains(s.RecipeId) &&
                        !recentRecipeIds.Contains(s.RecipeId));

                if (bestRecipe is null) continue;

                assignedRecipeIds.Add(bestRecipe.RecipeId);
                slots.Add(new MealPlanSlot
                {
                    DayOfWeek = day,
                    Id = Guid.NewGuid(),
                    MealSlot = mealSlot,
                    RecipeId = bestRecipe.RecipeId
                });
            }
        }

        // Claude review (stub passes through unchanged)
        var slotCandidates = slots.Select(s =>
        {
            var scored = scoredRecipes.First(sr => sr.RecipeId == s.RecipeId);
            return new MealPlanSlotCandidate
            {
                DayOfWeek = s.DayOfWeek,
                MealSlot = s.MealSlot,
                RecipeId = s.RecipeId,
                RecipeTitle = scored.Title,
                Score = scored.FinalScore,
                SeasonalAffinity = scored.SeasonalAffinity,
                WasteReductionScore = scored.WasteBonus
            };
        }).ToList();

        var expiringItems = inventory.Where(i =>
            i.ExpiryDate.HasValue && i.ExpiryDate.Value <= today.AddDays(ExpiryBonusDays)).ToList();

        // MEP-032: skip the Claude variety-and-waste-optimization pass when no
        // API key is configured. The deterministic ranking above drives
        // selection; the plan persists normally.
        IReadOnlyList<MealPlanSlotCandidate> optimized;
        if (await claudeAvailability.IsConfiguredAsync(cancellationToken))
        {
            try { optimized = await claudeService.OptimizeMealPlanAsync(slotCandidates, expiringItems, cancellationToken); }
            catch { optimized = slotCandidates; }
        }
        else
        {
            optimized = slotCandidates;
        }

        // Apply Claude's reordering if it changed anything
        var optimizedSlots = optimized.Select(c => new MealPlanSlot
        {
            DayOfWeek = c.DayOfWeek,
            Id = Guid.NewGuid(),
            MealSlot = c.MealSlot,
            RecipeId = c.RecipeId
        }).ToList();

        // Persist
        var mealPlan = new Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = name,
            WeekStartDate = weekStart
        };
        dbContext.MealPlans.Add(mealPlan);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var slot in optimizedSlots)
        {
            slot.MealPlanId = mealPlan.Id;
            dbContext.MealPlanSlots.Add(slot);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildResponseAsync(mealPlan.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MealPlanResponse?> GetActiveMealPlanAsync(CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.MealPlans
            .OrderByDescending(mp => mp.WeekStartDate)
            .ThenByDescending(mp => mp.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return plan is null ? null : await BuildResponseAsync(plan.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MealPlanResponse?> GetMealPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.MealPlans.AnyAsync(mp => mp.Id == id, cancellationToken);
        return exists ? await BuildResponseAsync(id, cancellationToken) : null;
    }

    /// <inheritdoc />
    public async Task<List<MealPlanResponse>> ListMealPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await dbContext.MealPlans
            .Include(mp => mp.Slots).ThenInclude(s => s.Recipe)
            .OrderByDescending(mp => mp.WeekStartDate)
            .ToListAsync(cancellationToken);

        return plans.Select(MapToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<MealPlanSlotResponse?> SwapSlotAsync(Guid slotId, SwapSlotRequest request, CancellationToken cancellationToken = default)
    {
        var slot = await dbContext.MealPlanSlots
            .FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken);

        if (slot is null) return null;

        var recipe = await dbContext.Recipes
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken);

        if (recipe is null) return null;

        slot.RecipeId = request.RecipeId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MealPlanSlotResponse
        {
            ConsumedAt = slot.ConsumedAt,
            CuisineType = recipe.CuisineType,
            DayOfWeek = slot.DayOfWeek,
            Id = slot.Id,
            MealSlot = slot.MealSlot,
            RecipeId = recipe.Id,
            RecipeTitle = recipe.Title
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<MealPlanResponse> BuildResponseAsync(Guid mealPlanId, CancellationToken cancellationToken)
    {
        var plan = await dbContext.MealPlans
            .Include(mp => mp.Slots).ThenInclude(s => s.Recipe)
            .FirstAsync(mp => mp.Id == mealPlanId, cancellationToken);

        return MapToResponse(plan);
    }

    private static Dictionary<DayOfWeek, List<MealSlot>> BuildDefaultSlotPreferences()
    {
        var days = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
        return days.ToDictionary(d => d, _ => new List<MealSlot> { MealSlot.Lunch, MealSlot.Dinner });
    }

    private static DateOnly GetCurrentWeekMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }

    private async Task<HashSet<Guid>> GetRecentRecipeIdsAsync(DateOnly weekStart, CancellationToken cancellationToken)
    {
        var sevenDaysAgo = weekStart.AddDays(-7);
        var recentSlots = await dbContext.MealPlanSlots
            .AsNoTracking()
            .Include(s => s.MealPlan)
            .Where(s => s.MealPlan.WeekStartDate >= sevenDaysAgo && s.MealPlan.WeekStartDate < weekStart)
            .Select(s => s.RecipeId)
            .ToListAsync(cancellationToken);
        return recentSlots.ToHashSet();
    }

    private async Task<List<Recipe>> LoadCandidateRecipesAsync(GenerateMealPlanRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.Recipes.AsNoTracking()
            .Include(r => r.DietaryTags)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.CanonicalIngredient).ThenInclude(ci => ci.SeasonalityWindows)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.UnitOfMeasure)
            .Where(r => r.RecipeIngredients.All(ri => ri.IsContainerResolved) && r.RecipeIngredients.Any());

        if (request.DietaryTags is { Count: > 0 })
        {
            foreach (var tag in request.DietaryTags)
            {
                var t = tag;
                query = query.Where(r => r.DietaryTags.Any(dt => dt.Tag == t));
            }
        }

        var recipes = await query.ToListAsync(cancellationToken);

        if (request.SeasonalOnly)
        {
            var currentMonth = (Month)DateTime.UtcNow.Month;
            recipes = recipes.Where(r => r.RecipeIngredients.Any(ri =>
                ri.CanonicalIngredient.SeasonalityWindows.Any(sw =>
                    SeasonalityHelper.IsInSeason(currentMonth, sw.PeakSeasonStart, sw.PeakSeasonEnd)))).ToList();
        }

        return recipes;
    }

    private static MealPlanResponse MapToResponse(Models.Entities.MealPlan plan)
    {
        return new MealPlanResponse
        {
            CreatedAt = plan.CreatedAt,
            Id = plan.Id,
            Name = plan.Name,
            Slots = plan.Slots
                .OrderBy(s => ((int)s.DayOfWeek + 6) % 7)
                .ThenBy(s => s.MealSlot)
                .Select(s => new MealPlanSlotResponse
                {
                    ConsumedAt = s.ConsumedAt,
                    CuisineType = s.Recipe.CuisineType,
                    DayOfWeek = s.DayOfWeek,
                    Id = s.Id,
                    MealSlot = s.MealSlot,
                    RecipeId = s.RecipeId,
                    RecipeTitle = s.Recipe.Title
                })
                .ToList(),
            WeekStartDate = plan.WeekStartDate
        };
    }

    private ScoredRecipe ScoreRecipe(
        Recipe recipe, Dictionary<Guid, List<InventoryBaseEntry>> inventoryBase, DateOnly today)
    {
        var matchedCount = 0;
        var wasteBonus = 0m;
        var seasonalAffinity = false;
        var currentMonth = (Month)today.Month;

        foreach (var ri in recipe.RecipeIngredients)
        {
            if (inventoryBase.TryGetValue(ri.CanonicalIngredientId, out var entries))
            {
                var compatible = entries.Where(e => ri.UnitOfMeasure != null && e.UnitOfMeasureType == ri.UnitOfMeasure.UnitOfMeasureType).ToList();
                if (compatible.Sum(e => e.BaseQuantity) > 0)
                    matchedCount++;

                if (compatible.Any(e => e.ExpiryDate.HasValue && e.ExpiryDate.Value <= today.AddDays(ExpiryBonusDays)))
                    wasteBonus += ExpiryBonusPerIngredient;
            }

            if (ri.CanonicalIngredient.SeasonalityWindows.Any(sw =>
                    SeasonalityHelper.IsInSeason(currentMonth, sw.PeakSeasonStart, sw.PeakSeasonEnd)))
                seasonalAffinity = true;
        }

        var total = recipe.RecipeIngredients.Count;
        var matchScore = total > 0 ? (decimal)matchedCount / total : 0m;
        var finalScore = matchScore + wasteBonus + (seasonalAffinity ? SeasonalAffinityBonus : 0m);

        return new ScoredRecipe
        {
            FinalScore = finalScore,
            MatchScore = matchScore,
            RecipeId = recipe.Id,
            SeasonalAffinity = seasonalAffinity,
            Title = recipe.Title,
            WasteBonus = wasteBonus
        };
    }

    private sealed class ScoredRecipe
    {
        public required decimal FinalScore { get; init; }
        public required decimal MatchScore { get; init; }
        public required Guid RecipeId { get; init; }
        public required bool SeasonalAffinity { get; init; }
        public required string Title { get; init; }
        public required decimal WasteBonus { get; init; }
    }
}
