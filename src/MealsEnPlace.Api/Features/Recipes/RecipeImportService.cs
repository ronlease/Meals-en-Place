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
    /// <summary>
    /// Maximum number of items that may be requested in a single page.
    /// Callers supplying a larger value are silently clamped to this limit.
    /// </summary>
    public const int MaxPageSize = 100;

    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

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

        // Increment RecipeReferenceCount for each referenced CanonicalIngredient
        // in the same SaveChanges call so the stored count stays consistent with
        // the new RecipeIngredient rows.  One recipe may reference the same canonical
        // ingredient more than once (e.g., "egg" in batter and in glaze), so group
        // by canonical id and add the full per-canonical count in one step.
        var countsByCanonicalId = recipe.RecipeIngredients
            .GroupBy(ri => ri.CanonicalIngredientId)
            .ToDictionary(g => g.Key, g => g.Count());

        if (countsByCanonicalId.Count > 0)
        {
            var canonicalsToUpdate = await dbContext.CanonicalIngredients
                .Where(c => countsByCanonicalId.Keys.Contains(c.Id))
                .ToListAsync(cancellationToken);

            foreach (var canonical in canonicalsToUpdate)
            {
                canonical.RecipeReferenceCount += countsByCanonicalId[canonical.Id];
            }
        }

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
    public async Task<PagedResult<RecipeListItemDto>> GetPagedLocalRecipesAsync(
        RecipeSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        // Clamp inputs to safe bounds — no 400 errors for out-of-range values.
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Detect the EF Core database provider once; ILike is Npgsql-only and
        // throws NotSupportedException on the in-memory provider used by tests.
        var isNpgsql = dbContext.Database.ProviderName == NpgsqlProviderName;

        IQueryable<Recipe> baseQuery = dbContext.Recipes.AsNoTracking();

        // Title search — uses the pg_trgm GIN index (IX_Recipes_Title_Trgm) via
        // ILike on Npgsql; falls back to a case-folded Contains for the EF Core
        // in-memory provider used by unit tests.
        if (!string.IsNullOrWhiteSpace(query.TitleSearch))
        {
            var term = query.TitleSearch.Trim();
            if (isNpgsql)
            {
                var pattern = "%" + IngredientSearchHelper.EscapeILikeWildcards(term) + "%";
                baseQuery = baseQuery.Where(r => EF.Functions.ILike(r.Title, pattern));
            }
            else
            {
                var lower = term.ToLowerInvariant();
                baseQuery = baseQuery.Where(r => r.Title.ToLower().Contains(lower));
            }
        }

        // Ingredient search — correlated EXISTS subquery against CanonicalIngredient.Name.
        // Uses the pg_trgm GIN index (IX_CanonicalIngredients_Name_Trgm) via ILike.
        if (!string.IsNullOrWhiteSpace(query.IngredientSearch))
        {
            var term = query.IngredientSearch.Trim();
            if (isNpgsql)
            {
                var pattern = "%" + IngredientSearchHelper.EscapeILikeWildcards(term) + "%";
                baseQuery = baseQuery.Where(r =>
                    r.RecipeIngredients.Any(ri =>
                        EF.Functions.ILike(ri.CanonicalIngredient.Name, pattern)));
            }
            else
            {
                var lower = term.ToLowerInvariant();
                baseQuery = baseQuery.Where(r =>
                    r.RecipeIngredients.Any(ri =>
                        ri.CanonicalIngredient.Name.ToLower().Contains(lower)));
            }
        }

        // Dietary-tag filter — AND semantics: each selected tag must be present.
        foreach (var tag in query.DietaryTags)
        {
            var capturedTag = tag;
            baseQuery = baseQuery.Where(r => r.DietaryTags.Any(dt => dt.Tag == capturedTag));
        }

        // COUNT(*) on the filtered set drives totalCount and totalPages metadata.
        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        // Project directly to the DTO in the database. No Include/ThenInclude
        // collection loads — DietaryTags and RecipeIngredient counts are
        // correlated subqueries that EF Core emits as scalar SELECT statements,
        // so no cartesian product forms. IsFullyResolved cannot be projected
        // from the C# computed property, so it is expressed as two Any()
        // subqueries that EF Core translates to SQL EXISTS clauses.
        var items = await baseQuery
            .OrderBy(r => r.Title)
            .Select(r => new RecipeListItemDto
            {
                CuisineType = r.CuisineType,
                DietaryTags = r.DietaryTags
                    .Select(dt => dt.Tag)
                    .OrderBy(t => t)
                    .ToList(),
                Id = r.Id,
                IsFullyResolved = r.RecipeIngredients.Any()
                    && r.RecipeIngredients.All(ri => ri.IsContainerResolved),
                Title = r.Title,
                TotalIngredients = r.RecipeIngredients.Count(),
                UnresolvedCount = r.RecipeIngredients.Count(ri => !ri.IsContainerResolved)
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RecipeListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
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
