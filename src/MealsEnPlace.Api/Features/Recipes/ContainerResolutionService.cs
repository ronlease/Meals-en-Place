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
