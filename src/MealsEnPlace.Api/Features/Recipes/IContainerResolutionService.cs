using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Recipes;

/// <summary>
/// Business logic contract for resolving container references on recipe ingredients.
/// Services work exclusively in metric base units; display conversion is applied
/// at the controller layer.
/// </summary>
public interface IContainerResolutionService
{
    /// <summary>
    /// Resolves every unresolved <see cref="RecipeIngredient"/> that shares the
    /// given canonical ingredient and notes phrase in a single transaction.
    /// Intended for high-volume review after a bulk ingest (MEP-026) when the
    /// same phrase (e.g. "1 can diced tomatoes") appears across hundreds of
    /// recipes and the user wants to declare its net weight once.
    /// </summary>
    /// <param name="canonicalIngredientId">Canonical ingredient shared by the group.</param>
    /// <param name="notes">
    /// The original import string that identifies the group (e.g. "1 can diced
    /// tomatoes"). Matched case-insensitively against <see cref="RecipeIngredient.Notes"/>.
    /// </param>
    /// <param name="quantity">The declared net weight or volume (positive decimal).</param>
    /// <param name="uomId">The <see cref="UnitOfMeasure"/> for the declared quantity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="BulkResolveResult"/> with the number of rows updated, or a
    /// validation error when the quantity is non-positive or the UOM does not exist.
    /// </returns>
    Task<BulkResolveResult> BulkResolveAsync(
        Guid canonicalIngredientId,
        string notes,
        decimal quantity,
        Guid uomId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all ingredients for <paramref name="recipeId"/> where
    /// <see cref="RecipeIngredient.IsContainerResolved"/> is false, or null if
    /// the recipe does not exist.
    /// </summary>
    /// <param name="recipeId">The recipe to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The list of unresolved <see cref="RecipeIngredient"/> rows,
    /// or null when the recipe is not found.
    /// </returns>
    Task<IReadOnlyList<RecipeIngredient>?> GetUnresolvedIngredientsAsync(
        Guid recipeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns unresolved container references grouped by canonical ingredient
    /// and normalized notes, ordered by occurrence count descending. Lets the
    /// user resolve "1 can diced tomatoes" once across every recipe that uses
    /// that phrase rather than opening each recipe individually.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<UnresolvedGroup>> GetUnresolvedGroupsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all recipes that contain at least one unresolved container reference,
    /// ordered alphabetically by title.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Recipe>> GetUnresolvedRecipesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a container reference on a recipe ingredient.
    /// Sets <see cref="RecipeIngredient.Quantity"/>, <see cref="RecipeIngredient.UomId"/>,
    /// and <see cref="RecipeIngredient.IsContainerResolved"/> = true.
    /// <see cref="RecipeIngredient.Notes"/> is preserved unchanged.
    /// </summary>
    /// <param name="recipeId">The recipe that owns the ingredient.</param>
    /// <param name="ingredientId">The <see cref="RecipeIngredient"/> to resolve.</param>
    /// <param name="request">The declared quantity and UOM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The updated <see cref="RecipeIngredient"/>, or null if the recipe or
    /// ingredient does not exist. Returns a validation error string when the
    /// UOM does not exist or the quantity is not positive.
    /// </returns>
    Task<ContainerResolutionResult> ResolveAsync(
        Guid recipeId,
        Guid ingredientId,
        ResolveContainerRequest request,
        CancellationToken cancellationToken = default);
}

