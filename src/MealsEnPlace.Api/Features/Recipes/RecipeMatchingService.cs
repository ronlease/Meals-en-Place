using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Implements the recipe matching pipeline.
/// </summary>
public class RecipeMatchingService(
    IClaudeAvailability claudeAvailability,
    IClaudeService claudeService,
    MealsEnPlaceDbContext dbContext,
    IUnitOfMeasureConversionService unitOfMeasureConversionService,
    UnitOfMeasureDisplayConverter unitOfMeasureDisplayConverter) : IRecipeMatchingService
{
    private const int NearMatchClaudeLimit = 10;
    private const int WasteBonusDays = 3;
    private const decimal WasteBonusPerIngredient = 0.1m;

    /// <inheritdoc />
    public async Task<RecipeMatchResponse> MatchRecipesAsync(RecipeMatchRequest request, CancellationToken cancellationToken = default)
    {
        var inventory = await InventoryBaseHelper.LoadInventoryAsync(dbContext, cancellationToken);
        var inventoryBase = await InventoryBaseHelper.ConvertToBaseUnitsAsync(inventory, unitOfMeasureConversionService, cancellationToken);
        var candidates = await LoadCandidateRecipesAsync(request, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var fullMatches = new List<RecipeMatchDto>();
        var nearMatches = new List<RecipeMatchDto>();
        var partialMatches = new List<RecipeMatchDto>();

        foreach (var recipe in candidates)
        {
            var result = await ScoreRecipeAsync(recipe, inventoryBase, today, cancellationToken);
            if (result is null) continue;

            switch (result.MatchTier)
            {
                case MatchTier.FullMatch: fullMatches.Add(result); break;
                case MatchTier.NearMatch: nearMatches.Add(result); break;
                case MatchTier.PartialMatch: partialMatches.Add(result); break;
            }
        }

        // MEP-032: skip the Claude feasibility / substitution pass entirely when
        // no API key is configured. The deterministic ranking above is still the
        // product of record; the response flag lets the UI show a subtle note.
        var claudeConfigured = await claudeAvailability.IsConfiguredAsync(cancellationToken);
        var enrichedNear = claudeConfigured
            ? await EnrichNearMatchesAsync(nearMatches, inventory, cancellationToken)
            : nearMatches;

        return new RecipeMatchResponse
        {
            ClaudeFeasibilityApplied = claudeConfigured,
            FullMatches = fullMatches.OrderByDescending(r => r.FinalScore).ToList(),
            NearMatches = enrichedNear.OrderByDescending(r => r.FinalScore).ToList(),
            PartialMatches = partialMatches.OrderByDescending(r => r.FinalScore).ToList()
        };
    }

    private async Task<List<RecipeMatchDto>> EnrichNearMatchesAsync(
        List<RecipeMatchDto> nearMatches, List<InventoryItem> inventory, CancellationToken cancellationToken)
    {
        var topCandidates = nearMatches.OrderByDescending(r => r.FinalScore).Take(NearMatchClaudeLimit).ToList();
        var enriched = new List<RecipeMatchDto>(nearMatches.Count);

        foreach (var dto in topCandidates)
        {
            var recipe = await dbContext.Recipes.AsNoTracking()
                .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.CanonicalIngredient)
                .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.UnitOfMeasure)
                .FirstOrDefaultAsync(r => r.Id == dto.RecipeId, cancellationToken);

            if (recipe is null) { enriched.Add(dto); continue; }

            var missingForClaude = dto.MissingIngredients
                .Select(m => new MissingIngredient { CanonicalIngredientName = m.IngredientName, RequiredQuantity = m.RequiredQuantity, RequiredUnitOfMeasure = m.RequiredUnitOfMeasure })
                .ToList();

            IReadOnlyList<SubstitutionSuggestion> suggestions;
            try { suggestions = await claudeService.SuggestSubstitutionsAsync(recipe, missingForClaude, inventory, cancellationToken); }
            catch { suggestions = []; }

            enriched.Add(dto with { SubstitutionSuggestions = suggestions });
        }

        enriched.AddRange(nearMatches.OrderByDescending(r => r.FinalScore).Skip(NearMatchClaudeLimit));
        return enriched;
    }

    private async Task<List<Recipe>> LoadCandidateRecipesAsync(RecipeMatchRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.Recipes.AsNoTracking()
            .Include(r => r.DietaryTags)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.CanonicalIngredient).ThenInclude(ci => ci.SeasonalityWindows)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.UnitOfMeasure)
            .Where(r => r.RecipeIngredients.All(ri => ri.IsContainerResolved) && r.RecipeIngredients.Any());

        if (!string.IsNullOrWhiteSpace(request.Cuisine))
        {
            var cuisine = request.Cuisine.Trim();
            query = query.Where(r => r.CuisineType.ToLower() == cuisine.ToLower());
        }

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

    private async Task<RecipeMatchDto?> ScoreRecipeAsync(
        Recipe recipe, Dictionary<Guid, List<InventoryBaseEntry>> inventoryBase,
        DateOnly today, CancellationToken cancellationToken)
    {
        var matched = new List<MatchedIngredientDto>();
        var missing = new List<MissingIngredientDto>();
        var wasteBonus = 0m;

        foreach (var ri in recipe.RecipeIngredients)
        {
            if (ri.UnitOfMeasureId is null || ri.UnitOfMeasure is null) { missing.Add(await BuildMissingDtoAsync(ri, cancellationToken)); continue; }

            var conv = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(ri.Quantity, ri.UnitOfMeasureId.Value, cancellationToken);
            if (!conv.Success) { missing.Add(await BuildMissingDtoAsync(ri, cancellationToken)); continue; }

            if (!inventoryBase.TryGetValue(ri.CanonicalIngredientId, out var entries))
            { missing.Add(await BuildMissingDtoAsync(ri, cancellationToken)); continue; }

            var compatible = entries.Where(e => e.UnitOfMeasureType == ri.UnitOfMeasure.UnitOfMeasureType).ToList();
            var totalAvailable = compatible.Sum(e => e.BaseQuantity);

            if (totalAvailable < conv.ConvertedQuantity)
            { missing.Add(await BuildMissingDtoAsync(ri, cancellationToken)); continue; }

            var isExpiring = compatible.Any(e => e.ExpiryDate.HasValue && e.ExpiryDate.Value <= today.AddDays(WasteBonusDays));
            if (isExpiring) wasteBonus += WasteBonusPerIngredient;

            var (dispReq, reqUnitOfMeasure) = await unitOfMeasureDisplayConverter.ConvertAsync(conv.ConvertedQuantity, ri.UnitOfMeasure.UnitOfMeasureType, cancellationToken);
            var (dispAvail, availUnitOfMeasure) = await unitOfMeasureDisplayConverter.ConvertAsync(totalAvailable, ri.UnitOfMeasure.UnitOfMeasureType, cancellationToken);

            matched.Add(new MatchedIngredientDto
            {
                AvailableQuantity = dispAvail,
                AvailableUnitOfMeasure = availUnitOfMeasure,
                IngredientName = ri.CanonicalIngredient.Name,
                IsExpiryImminent = isExpiring,
                RequiredQuantity = dispReq,
                RequiredUnitOfMeasure = reqUnitOfMeasure
            });
        }

        var total = recipe.RecipeIngredients.Count;
        if (total == 0) return null;

        var matchScore = (decimal)matched.Count / total;
        var finalScore = Math.Min(matchScore + wasteBonus, 1.0m);

        MatchTier tier;
        if (matchScore == 1.0m) tier = MatchTier.FullMatch;
        else if (matchScore >= 0.75m) tier = MatchTier.NearMatch;
        else if (matchScore >= 0.5m) tier = MatchTier.PartialMatch;
        else return null;

        return new RecipeMatchDto
        {
            CuisineType = recipe.CuisineType,
            FinalScore = finalScore,
            MatchedIngredients = matched,
            MatchScore = matchScore,
            MatchTier = tier,
            MissingIngredients = missing,
            RecipeId = recipe.Id,
            Title = recipe.Title
        };
    }

    private async Task<MissingIngredientDto> BuildMissingDtoAsync(RecipeIngredient ri, CancellationToken cancellationToken)
    {
        if (ri.UnitOfMeasureId is null || ri.UnitOfMeasure is null)
            return new MissingIngredientDto { IngredientName = ri.CanonicalIngredient.Name, RequiredQuantity = ri.Quantity, RequiredUnitOfMeasure = string.Empty };

        var conv = await unitOfMeasureConversionService.ConvertToBaseUnitsAsync(ri.Quantity, ri.UnitOfMeasureId.Value, cancellationToken);
        if (!conv.Success)
            return new MissingIngredientDto { IngredientName = ri.CanonicalIngredient.Name, RequiredQuantity = ri.Quantity, RequiredUnitOfMeasure = ri.UnitOfMeasure.Abbreviation };

        var (qty, unitOfMeasure) = await unitOfMeasureDisplayConverter.ConvertAsync(conv.ConvertedQuantity, ri.UnitOfMeasure.UnitOfMeasureType, cancellationToken);
        return new MissingIngredientDto { IngredientName = ri.CanonicalIngredient.Name, RequiredQuantity = qty, RequiredUnitOfMeasure = unitOfMeasure };
    }
}
