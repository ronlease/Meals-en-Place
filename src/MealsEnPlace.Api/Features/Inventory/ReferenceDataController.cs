using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Provides read-only reference data (canonical ingredients and units of measure)
/// for use by the Inventory dialog and other client-side forms that need to resolve
/// Guid foreign keys before submitting a request.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ReferenceDataController(MealsEnPlaceDbContext db) : ControllerBase
{
    /// <summary>
    /// Creates a new canonical ingredient.
    /// </summary>
    /// <remarks>
    /// Use this endpoint to add an ingredient that is not present in the seed data.
    /// The new ingredient is immediately available for inventory and recipe use.
    /// </remarks>
    /// <param name="request">Name, category, and default UOM for the new ingredient.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 201 with the created <see cref="CanonicalIngredientDto"/>;
    /// 400 if the name is blank or the <c>defaultUomId</c> does not reference an existing unit;
    /// 409 if an ingredient with the same name already exists.
    /// </returns>
    [HttpPost("ingredients")]
    [ProducesResponseType(typeof(CanonicalIngredientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateIngredient(
        [FromBody] CreateCanonicalIngredientRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails
            {
                Detail = "Name must not be blank.",
                Status = StatusCodes.Status400BadRequest,
                Title  = "Bad Request"
            });
        }

        var uomExists = await db.UnitsOfMeasure
            .AnyAsync(u => u.Id == request.DefaultUomId, cancellationToken);

        if (!uomExists)
        {
            return BadRequest(new ProblemDetails
            {
                Detail = $"Unit of measure '{request.DefaultUomId}' was not found.",
                Status = StatusCodes.Status400BadRequest,
                Title  = "Bad Request"
            });
        }

        var duplicate = await db.CanonicalIngredients
            .AnyAsync(c => c.Name == request.Name, cancellationToken);

        if (duplicate)
        {
            return Conflict(new ProblemDetails
            {
                Detail = $"A canonical ingredient named '{request.Name}' already exists.",
                Status = StatusCodes.Status409Conflict,
                Title  = "Conflict"
            });
        }

        var ingredient = new CanonicalIngredient
        {
            Category     = request.Category,
            DefaultUomId = request.DefaultUomId,
            Id           = Guid.NewGuid(),
            Name         = request.Name.Trim()
        };

        db.CanonicalIngredients.Add(ingredient);
        await db.SaveChangesAsync(cancellationToken);

        var dto = MapIngredient(ingredient);
        return CreatedAtAction(nameof(ListIngredients), dto);
    }

    /// <summary>
    /// Returns all canonical ingredients, ordered by name.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="CanonicalIngredientDto"/> records.</returns>
    [HttpGet("ingredients")]
    [ProducesResponseType(typeof(IReadOnlyList<CanonicalIngredientDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CanonicalIngredientDto>>> ListIngredients(
        CancellationToken cancellationToken)
    {
        var ingredients = await db.CanonicalIngredients
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CanonicalIngredientDto
            {
                Category     = c.Category,
                DefaultUomId = c.DefaultUomId,
                Id           = c.Id,
                Name         = c.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(ingredients);
    }

    /// <summary>
    /// Returns all units of measure, ordered by name.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="UnitOfMeasureDto"/> records.</returns>
    [HttpGet("units")]
    [ProducesResponseType(typeof(IReadOnlyList<UnitOfMeasureDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UnitOfMeasureDto>>> ListUnits(
        CancellationToken cancellationToken)
    {
        var units = await db.UnitsOfMeasure
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UnitOfMeasureDto
            {
                Abbreviation = u.Abbreviation,
                Id           = u.Id,
                Name         = u.Name,
                UomType      = u.UomType
            })
            .ToListAsync(cancellationToken);

        return Ok(units);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static CanonicalIngredientDto MapIngredient(CanonicalIngredient ingredient) =>
        new()
        {
            Category     = ingredient.Category,
            DefaultUomId = ingredient.DefaultUomId,
            Id           = ingredient.Id,
            Name         = ingredient.Name
        };
}
