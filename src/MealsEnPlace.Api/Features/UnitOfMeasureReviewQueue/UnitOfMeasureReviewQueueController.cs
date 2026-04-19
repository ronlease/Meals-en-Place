using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.UnitOfMeasureReviewQueue;

/// <summary>
/// Endpoints for the unit-of-measure review queue introduced by MEP-026. Lets
/// the user inspect unresolved unit tokens captured during bulk ingest and
/// decide how each should be handled: map to an existing canonical
/// <see cref="UnitOfMeasure"/> (creates a new <see cref="UnitOfMeasureAlias"/>),
/// or ignore the token permanently so it is not re-surfaced.
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/v1/unit-of-measure-review-queue")]
public class UnitOfMeasureReviewQueueController(MealsEnPlaceDbContext dbContext) : ControllerBase
{
    /// <summary>Lists every unresolved unit-of-measure token awaiting review.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 200 with the queue ordered by Count descending, then LastSeenAt descending,
    /// so the most-frequently-seen tokens surface first.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UnresolvedUnitOfMeasureTokenResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UnresolvedUnitOfMeasureTokenResponse>>> List(
        CancellationToken cancellationToken = default)
    {
        var queue = await dbContext.UnresolvedUnitOfMeasureTokens
            .AsNoTracking()
            .OrderByDescending(t => t.Count)
            .ThenByDescending(t => t.LastSeenAt)
            .Select(t => new UnresolvedUnitOfMeasureTokenResponse
            {
                Count = t.Count,
                FirstSeenAt = t.FirstSeenAt,
                Id = t.Id,
                LastSeenAt = t.LastSeenAt,
                SampleIngredientContext = t.SampleIngredientContext,
                SampleMeasureString = t.SampleMeasureString,
                UnitToken = t.UnitToken
            })
            .ToListAsync(cancellationToken);

        return Ok(queue);
    }

    /// <summary>
    /// Maps an unresolved token to an existing <see cref="UnitOfMeasure"/>, creates
    /// a <see cref="UnitOfMeasureAlias"/> so future occurrences resolve deterministically,
    /// and removes the queue row.
    /// </summary>
    /// <param name="id">The queue row id.</param>
    /// <param name="request">Target unit-of-measure id and optional override flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 200 with the created alias id;
    /// 404 if the queue row or <see cref="UnitOfMeasure"/> does not exist;
    /// 409 if an alias with the same text already exists and <see cref="MapTokenToUnitOfMeasureRequest.AllowDuplicateAlias"/> is false.
    /// </returns>
    [HttpPost("{id:guid}/map")]
    [ProducesResponseType(typeof(MapTokenToUnitOfMeasureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MapTokenToUnitOfMeasureResponse>> Map(
        [FromRoute] Guid id,
        [FromBody] MapTokenToUnitOfMeasureRequest request,
        CancellationToken cancellationToken = default)
    {
        var queueRow = await dbContext.UnresolvedUnitOfMeasureTokens
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (queueRow is null)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"No unit-of-measure review queue row with id '{id}'.",
                Status = StatusCodes.Status404NotFound,
                Title = "Queue row not found"
            });
        }

        var targetUnitOfMeasure = await dbContext.UnitsOfMeasure
            .FirstOrDefaultAsync(u => u.Id == request.UnitOfMeasureId, cancellationToken);

        if (targetUnitOfMeasure is null)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"No UnitOfMeasure with id '{request.UnitOfMeasureId}'.",
                Status = StatusCodes.Status404NotFound,
                Title = "UnitOfMeasure not found"
            });
        }

        // Service-layer uniqueness: reject duplicate alias text unless the caller
        // explicitly opts in via AllowDuplicateAlias. See MEP-026 and the DB-level
        // decision note on UnitOfMeasureAlias.
        var existingAlias = await dbContext.UnitOfMeasureAliases
            .FirstOrDefaultAsync(a => a.Alias == queueRow.UnitToken, cancellationToken);

        if (existingAlias is not null && !request.AllowDuplicateAlias)
        {
            return Conflict(new ProblemDetails
            {
                Detail = $"Alias '{queueRow.UnitToken}' already exists and maps to UnitOfMeasureId '{existingAlias.UnitOfMeasureId}'. " +
                         "Resubmit with allowDuplicateAlias=true to force a second row (needed only for legitimate case-sensitive variants).",
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate alias"
            });
        }

        var newAlias = new UnitOfMeasureAlias
        {
            Alias = queueRow.UnitToken,
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            UnitOfMeasureId = targetUnitOfMeasure.Id
        };

        dbContext.UnitOfMeasureAliases.Add(newAlias);
        dbContext.UnresolvedUnitOfMeasureTokens.Remove(queueRow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new MapTokenToUnitOfMeasureResponse
        {
            AliasId = newAlias.Id,
            AliasText = newAlias.Alias,
            UnitOfMeasureId = targetUnitOfMeasure.Id
        });
    }

    /// <summary>
    /// Removes an unresolved token from the queue without creating an alias.
    /// Future occurrences of the same token will re-queue -- use this when the
    /// token is garbage (typo in source data, OCR artifact) rather than a real
    /// unit the user wants to permanently reject.
    /// </summary>
    /// <param name="id">The queue row id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 if the queue row does not exist.
    /// </returns>
    [HttpPost("{id:guid}/ignore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ignore(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var queueRow = await dbContext.UnresolvedUnitOfMeasureTokens
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (queueRow is null)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"No unit-of-measure review queue row with id '{id}'.",
                Status = StatusCodes.Status404NotFound,
                Title = "Queue row not found"
            });
        }

        dbContext.UnresolvedUnitOfMeasureTokens.Remove(queueRow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
