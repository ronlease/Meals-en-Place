using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Manages the local recipe library: manual creation (MEP-018), retrieval, and
/// listing. The TheMealDB search and import paths that previously lived here
/// were removed under MEP-033 once the Kaggle bulk ingest (MEP-026) became the
/// catalog source; the <c>MealsEnPlace.Tools.Ingest</c> tool now supplies
/// recipes in bulk, and this service covers the interactive per-recipe flow.
/// </summary>
public sealed class RecipeImportService(
    IClaudeAvailability claudeAvailability,
    IClaudeService claudeService,
    MealsEnPlaceDbContext dbContext,
    ILogger<RecipeImportService> logger) : IRecipeImportService
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
            Title = InputSanitizer.SanitizeForStorage(request.Title, 200) ?? string.Empty
        };

        foreach (var ing in request.Ingredients)
        {
            var isResolved = ing.UnitOfMeasureId.HasValue;
            var detectionResult = ContainerReferenceDetector.Detect(ing.Notes);

            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ing.CanonicalIngredientId,
                Id = Guid.NewGuid(),
                IsContainerResolved = isResolved && !detectionResult.IsContainerReference,
                Notes = InputSanitizer.SanitizeForStorage(ing.Notes, 500),
                Quantity = ing.Quantity,
                RecipeId = recipe.Id,
                UnitOfMeasureId = ing.UnitOfMeasureId
            });
        }

        dbContext.Recipes.Add(recipe);
        await dbContext.SaveChangesAsync(cancellationToken);

        // MEP-032: skip dietary classification entirely when no Claude key is
        // configured. The recipe is persisted with an empty RecipeDietaryTag
        // collection; the user can tag manually via the recipe edit UI.
        if (await claudeAvailability.IsConfiguredAsync(cancellationToken))
        {
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
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.UnitOfMeasure)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return recipe is null ? null : MapToDetailDto(recipe);
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
                    UnitOfMeasureAbbreviation = ri.UnitOfMeasure?.Abbreviation ?? string.Empty,
                    UnitOfMeasureId = ri.UnitOfMeasureId
                })
                .ToList(),
            Instructions = recipe.Instructions,
            IsFullyResolved = recipe.IsFullyResolved,
            ServingCount = recipe.ServingCount,
            SourceUrl = recipe.SourceUrl,
            Title = recipe.Title
        };
}
