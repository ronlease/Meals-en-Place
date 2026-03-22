using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Implements TheMealDB search and recipe import pipeline.
/// </summary>
public sealed class RecipeImportService(
    IClaudeService claudeService,
    MealsEnPlaceDbContext dbContext,
    ILogger<RecipeImportService> logger,
    ITheMealDbClient theMealDbClient,
    IUomNormalizationService uomNormalizationService) : IRecipeImportService
{
    /// <inheritdoc />
    public async Task<RecipeDetailDto> CreateRecipeAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        var recipe = new Recipe
        {
            CuisineType = InputSanitizer.SanitizeForStorage(request.CuisineType, 100) ?? string.Empty,
            Id = Guid.NewGuid(),
            Instructions = InputSanitizer.SanitizeForStorage(request.Instructions, 5000) ?? string.Empty,
            ServingCount = request.ServingCount,
            SourceUrl = null,
            TheMealDbId = null,
            Title = InputSanitizer.SanitizeForStorage(request.Title, 200) ?? string.Empty
        };

        foreach (var ing in request.Ingredients)
        {
            var isResolved = ing.UomId.HasValue;
            var detectionResult = ContainerReferenceDetector.Detect(ing.Notes);

            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ing.CanonicalIngredientId,
                Id = Guid.NewGuid(),
                IsContainerResolved = isResolved && !detectionResult.IsContainerReference,
                Notes = InputSanitizer.SanitizeForStorage(ing.Notes, 500),
                Quantity = ing.Quantity,
                RecipeId = recipe.Id,
                UomId = ing.UomId
            });
        }

        dbContext.Recipes.Add(recipe);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var dietaryTags = await claudeService.ClassifyDietaryTagsAsync(recipe);
            foreach (var tag in dietaryTags)
            {
                dbContext.RecipeDietaryTags.Add(new RecipeDietaryTag
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Tag = tag
                });
            }
            if (dietaryTags.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude dietary classification failed for '{Title}'.", InputSanitizer.SanitizeForLogging(recipe.Title));
        }

        return (await GetRecipeDetailAsync(recipe.Id, cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecipeListItemDto>> GetAllLocalRecipesAsync(CancellationToken cancellationToken = default)
    {
        var recipes = await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.DietaryTags)
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.CanonicalIngredient)
            .OrderBy(r => r.Title)
            .ToListAsync(cancellationToken);

        return recipes.Select(r =>
        {
            var unresolved = r.RecipeIngredients.Count(ri => !ri.IsContainerResolved);
            return new RecipeListItemDto
            {
                CuisineType = r.CuisineType,
                DietaryTags = r.DietaryTags.Select(dt => dt.Tag).OrderBy(t => t).ToList(),
                Id = r.Id,
                IngredientNames = r.RecipeIngredients
                    .Select(ri => ri.CanonicalIngredient.Name)
                    .OrderBy(n => n)
                    .ToList(),
                IsFullyResolved = r.IsFullyResolved,
                Title = r.Title,
                TotalIngredients = r.RecipeIngredients.Count,
                UnresolvedCount = unresolved
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<RecipeDetailDto?> GetRecipeDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recipe = await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.DietaryTags)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.CanonicalIngredient)
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Uom)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return recipe is null ? null : MapToDetailDto(recipe);
    }

    /// <inheritdoc />
    public async Task<RecipeImportResultDto> ImportByIdAsync(string mealDbId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TheMealDbId == mealDbId, cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Recipe with TheMealDB ID '{mealDbId}' has already been imported (local ID: {existing.Id}).");
        }

        var meal = await theMealDbClient.GetByIdAsync(mealDbId, cancellationToken)
            ?? throw new InvalidOperationException($"No meal found in TheMealDB for ID '{mealDbId}'.");

        var recipe = new Recipe
        {
            CuisineType = meal.Area ?? string.Empty,
            Id = Guid.NewGuid(),
            Instructions = meal.Instructions ?? string.Empty,
            ServingCount = 4,
            SourceUrl = meal.Source,
            TheMealDbId = meal.MealId,
            Title = meal.MealName ?? string.Empty
        };

        foreach (var (ingredientName, measureString) in meal.GetIngredientMeasurePairs())
        {
            var canonicalIngredient = await FindOrCreateCanonicalIngredientAsync(ingredientName, cancellationToken);
            var detectionResult = ContainerReferenceDetector.Detect(measureString);

            RecipeIngredient recipeIngredient;
            if (detectionResult.IsContainerReference)
            {
                recipeIngredient = new RecipeIngredient
                {
                    CanonicalIngredientId = canonicalIngredient.Id,
                    Id = Guid.NewGuid(),
                    IsContainerResolved = false,
                    Notes = string.IsNullOrWhiteSpace(measureString) ? $"1 {detectionResult.DetectedKeyword} {ingredientName}" : measureString,
                    Quantity = 0m,
                    RecipeId = recipe.Id,
                    UomId = null
                };
            }
            else
            {
                var normalization = await uomNormalizationService.NormalizeAsync(
                    string.IsNullOrWhiteSpace(measureString) ? "1 ea" : measureString,
                    ingredientName, cancellationToken);

                recipeIngredient = new RecipeIngredient
                {
                    CanonicalIngredientId = canonicalIngredient.Id,
                    Id = Guid.NewGuid(),
                    IsContainerResolved = true,
                    Notes = normalization.WasClaudeResolved ? normalization.Notes : null,
                    Quantity = normalization.Quantity,
                    RecipeId = recipe.Id,
                    UomId = normalization.UomId == Guid.Empty ? canonicalIngredient.DefaultUomId : normalization.UomId
                };
            }

            recipe.RecipeIngredients.Add(recipeIngredient);
        }

        dbContext.Recipes.Add(recipe);
        await dbContext.SaveChangesAsync(cancellationToken);

        IReadOnlyList<DietaryTag> dietaryTags;
        try
        {
            dietaryTags = await claudeService.ClassifyDietaryTagsAsync(recipe);
            foreach (var tag in dietaryTags)
            {
                dbContext.RecipeDietaryTags.Add(new RecipeDietaryTag
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Tag = tag
                });
            }
            if (dietaryTags.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude dietary classification failed for '{Title}'.", InputSanitizer.SanitizeForLogging(recipe.Title));
            dietaryTags = [];
        }

        return new RecipeImportResultDto
        {
            DietaryTags = dietaryTags,
            RecipeId = recipe.Id,
            Title = recipe.Title,
            TotalIngredients = recipe.RecipeIngredients.Count,
            UnresolvedCount = recipe.RecipeIngredients.Count(ri => !ri.IsContainerResolved)
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecipeSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var meals = await theMealDbClient.SearchByNameAsync(query, cancellationToken);
        return await MapToSearchResultsAsync(meals, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecipeSearchResultDto>> SearchByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var meals = await theMealDbClient.FilterByCategoryAsync(category, cancellationToken);
        return await MapToSearchResultsAsync(meals, cancellationToken);
    }

    private static RecipeDetailDto MapToDetailDto(Recipe recipe) =>
        new()
        {
            CuisineType = recipe.CuisineType,
            DietaryTags = recipe.DietaryTags.Select(dt => dt.Tag.ToString()).OrderBy(t => t).ToList(),
            Id = recipe.Id,
            Ingredients = recipe.RecipeIngredients
                .OrderBy(ri => ri.CanonicalIngredient.Name)
                .Select(ri => new RecipeIngredientDetailDto
                {
                    CanonicalIngredientId = ri.CanonicalIngredientId,
                    Id = ri.Id,
                    IngredientName = ri.CanonicalIngredient.Name,
                    IsContainerResolved = ri.IsContainerResolved,
                    Notes = ri.Notes,
                    Quantity = ri.Quantity,
                    UomAbbreviation = ri.Uom?.Abbreviation ?? string.Empty,
                    UomId = ri.UomId
                })
                .ToList(),
            Instructions = recipe.Instructions,
            IsFullyResolved = recipe.IsFullyResolved,
            ServingCount = recipe.ServingCount,
            SourceUrl = recipe.SourceUrl,
            Title = recipe.Title
        };

    private async Task<CanonicalIngredient> FindOrCreateCanonicalIngredientAsync(string ingredientName, CancellationToken cancellationToken)
    {
        var normalized = InputSanitizer.SanitizeForStorage(ingredientName, 200) ?? ingredientName.Trim();
        var existing = await dbContext.CanonicalIngredients
            .FirstOrDefaultAsync(ci => ci.Name.ToLower() == normalized.ToLower(), cancellationToken);

        if (existing is not null) return existing;

        var eachUom = await dbContext.UnitsOfMeasure.FirstAsync(u => u.Abbreviation == "ea", cancellationToken);
        var newIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUomId = eachUom.Id,
            Id = Guid.NewGuid(),
            Name = normalized
        };
        dbContext.CanonicalIngredients.Add(newIngredient);
        return newIngredient;
    }

    private async Task<IReadOnlyList<RecipeSearchResultDto>> MapToSearchResultsAsync(
        IReadOnlyList<TheMealDbMeal> meals, CancellationToken cancellationToken)
    {
        if (meals.Count == 0) return [];

        var mealDbIds = meals.Where(m => m.MealId is not null).Select(m => m.MealId!).ToList();
        var importedIds = await dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.TheMealDbId != null && mealDbIds.Contains(r.TheMealDbId))
            .Select(r => r.TheMealDbId!)
            .ToHashSetAsync(cancellationToken);

        return meals
            .Where(m => m.MealId is not null)
            .Select(m => new RecipeSearchResultDto
            {
                AlreadyImported = importedIds.Contains(m.MealId!),
                Category = m.Category ?? string.Empty,
                Id = m.MealId!,
                Thumbnail = m.MealThumb,
                Title = m.MealName ?? string.Empty
            })
            .ToList();
    }
}
