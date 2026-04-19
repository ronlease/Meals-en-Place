using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// EF Core implementation of <see cref="IContainerResolutionService"/>.
/// All quantity storage and comparison runs in metric base units.
/// Display conversion is applied at the controller layer.
/// </summary>
public class ContainerResolutionService(MealsEnPlaceDbContext dbContext)
    : IContainerResolutionService
{
    /// <inheritdoc />
    public async Task<BulkResolveResult> BulkResolveAsync(
        Guid canonicalIngredientId,
        string notes,
        decimal quantity,
        Guid uomId,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0m)
        {
            return BulkResolveResult.ValidationError("Quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            return BulkResolveResult.ValidationError("Notes must be a non-empty string.");
        }

        var uomExists = await dbContext.UnitsOfMeasure
            .AsNoTracking()
            .AnyAsync(u => u.Id == uomId, cancellationToken);

        if (!uomExists)
        {
            return BulkResolveResult.ValidationError($"Unit of measure '{uomId}' was not found.");
        }

        var normalized = notes.Trim().ToLower();
        var matches = await dbContext.RecipeIngredients
            .Where(ri =>
                ri.CanonicalIngredientId == canonicalIngredientId
                && !ri.IsContainerResolved
                && ri.Notes != null
                && ri.Notes.Trim().ToLower() == normalized)
            .ToListAsync(cancellationToken);

        foreach (var ingredient in matches)
        {
            ingredient.IsContainerResolved = true;
            ingredient.Quantity = quantity;
            ingredient.UomId = uomId;
            // Notes preserved verbatim -- it is the audit trail for what was resolved.
        }

        if (matches.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return BulkResolveResult.Success(matches.Count);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnresolvedGroup>> GetUnresolvedGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        // Group unresolved ingredients by (CanonicalIngredientId, Notes). Notes
        // is normalized to lower-case trimmed form for the grouping key so that
        // "1 can diced tomatoes" and " 1 can diced tomatoes " collapse into one
        // group. The displayed Notes is the trimmed form.
        var grouped = await dbContext.RecipeIngredients
            .AsNoTracking()
            .Where(ri => !ri.IsContainerResolved && ri.Notes != null)
            .Select(ri => new
            {
                ri.CanonicalIngredientId,
                CanonicalIngredientName = ri.CanonicalIngredient.Name,
                DisplayNotes = ri.Notes!.Trim(),
                NormalizedNotes = ri.Notes!.Trim().ToLower()
            })
            .GroupBy(x => new { x.CanonicalIngredientId, x.NormalizedNotes })
            .Select(g => new UnresolvedGroup
            {
                CanonicalIngredientId = g.Key.CanonicalIngredientId,
                CanonicalIngredientName = g.Select(x => x.CanonicalIngredientName).First(),
                Notes = g.Select(x => x.DisplayNotes).First(),
                OccurrenceCount = g.Count()
            })
            .OrderByDescending(g => g.OccurrenceCount)
            .ThenBy(g => g.CanonicalIngredientName)
            .ToListAsync(cancellationToken);

        return grouped;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecipeIngredient>?> GetUnresolvedIngredientsAsync(
        Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        var recipeExists = await dbContext.Recipes
            .AsNoTracking()
            .AnyAsync(r => r.Id == recipeId, cancellationToken);

        if (!recipeExists)
        {
            return null;
        }

        return await dbContext.RecipeIngredients
            .AsNoTracking()
            .Include(ri => ri.CanonicalIngredient)
            .Where(ri => ri.RecipeId == recipeId && !ri.IsContainerResolved)
            .OrderBy(ri => ri.CanonicalIngredient.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Recipe>> GetUnresolvedRecipesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Recipes
            .AsNoTracking()
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.CanonicalIngredient)
            .Where(r => r.RecipeIngredients.Any(ri => !ri.IsContainerResolved))
            .OrderBy(r => r.Title)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContainerResolutionResult> ResolveAsync(
        Guid recipeId,
        Guid ingredientId,
        ResolveContainerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            return ContainerResolutionResult.ValidationError(
                "Quantity must be greater than zero.");
        }

        var recipeExists = await dbContext.Recipes
            .AsNoTracking()
            .AnyAsync(r => r.Id == recipeId, cancellationToken);

        if (!recipeExists)
        {
            return ContainerResolutionResult.RecipeNotFound();
        }

        var uomExists = await dbContext.UnitsOfMeasure
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UomId, cancellationToken);

        if (!uomExists)
        {
            return ContainerResolutionResult.ValidationError(
                $"Unit of measure '{request.UomId}' was not found.");
        }

        var ingredient = await dbContext.RecipeIngredients
            .Include(ri => ri.CanonicalIngredient)
            .Include(ri => ri.Uom)
            .FirstOrDefaultAsync(
                ri => ri.Id == ingredientId && ri.RecipeId == recipeId,
                cancellationToken);

        if (ingredient is null)
        {
            return ContainerResolutionResult.IngredientNotFound();
        }

        ingredient.IsContainerResolved = true;
        ingredient.Quantity = request.Quantity;
        ingredient.UomId = request.UomId;
        // Notes is intentionally left unchanged — it preserves the original import string.

        await dbContext.SaveChangesAsync(cancellationToken);

        // Re-load navigation so the controller can access Uom.UomType for display conversion.
        await dbContext.Entry(ingredient)
            .Reference(ri => ri.Uom)
            .LoadAsync(cancellationToken);

        return ContainerResolutionResult.Success(ingredient);
    }
}
