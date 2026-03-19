using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Preferences;

/// <summary>
/// User preferences endpoints — reads and updates the single-row preferences table.
/// </summary>
[ApiController]
[Route("api/v1/preferences")]
[Produces("application/json")]
public class UserPreferencesController(MealsEnPlaceDbContext dbContext) : ControllerBase
{
    private static readonly Guid FixedRowId = new("d1000000-0000-0000-0000-000000000001");

    /// <summary>Returns the current user preferences.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the current display system preference. Returns Imperial if no row exists.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(UserPreferencesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserPreferencesResponse>> Get(CancellationToken cancellationToken = default)
    {
        var prefs = await dbContext.UserPreferences
            .FirstOrDefaultAsync(cancellationToken);

        var displaySystem = prefs?.DisplaySystem ?? DisplaySystem.Imperial;

        return Ok(new UserPreferencesResponse
        {
            DisplaySystem = displaySystem.ToString()
        });
    }

    /// <summary>Updates the user's display unit system preference.</summary>
    /// <param name="request">The desired display system value: "Imperial" or "Metric".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the updated preferences; 400 if the value is not a valid DisplaySystem.</returns>
    [HttpPut]
    [ProducesResponseType(typeof(UserPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPreferencesResponse>> Update(
        [FromBody] UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<DisplaySystem>(request.DisplaySystem, ignoreCase: true, out var displaySystem))
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Detail = $"'{request.DisplaySystem}' is not a valid DisplaySystem. Use 'Imperial' or 'Metric'."
            });
        }

        var prefs = await dbContext.UserPreferences
            .FirstOrDefaultAsync(cancellationToken);

        if (prefs is null)
        {
            prefs = new UserPreferences
            {
                DisplaySystem = displaySystem,
                Id = FixedRowId
            };
            dbContext.UserPreferences.Add(prefs);
        }
        else
        {
            prefs.DisplaySystem = displaySystem;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new UserPreferencesResponse
        {
            DisplaySystem = prefs.DisplaySystem.ToString()
        });
    }
}
