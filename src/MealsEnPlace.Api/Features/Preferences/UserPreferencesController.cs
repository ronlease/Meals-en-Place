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

        return Ok(new UserPreferencesResponse
        {
            AutoDepleteOnConsume = prefs?.AutoDepleteOnConsume ?? false,
            DisplaySystem = (prefs?.DisplaySystem ?? DisplaySystem.Imperial).ToString()
        });
    }

    /// <summary>Updates the user's preferences.</summary>
    /// <param name="request">DisplaySystem is required; AutoDepleteOnConsume is optional.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the updated preferences; 400 if the DisplaySystem value is invalid.</returns>
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
                AutoDepleteOnConsume = request.AutoDepleteOnConsume ?? false,
                DisplaySystem = displaySystem,
                Id = FixedRowId
            };
            dbContext.UserPreferences.Add(prefs);
        }
        else
        {
            prefs.DisplaySystem = displaySystem;
            if (request.AutoDepleteOnConsume.HasValue)
            {
                prefs.AutoDepleteOnConsume = request.AutoDepleteOnConsume.Value;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new UserPreferencesResponse
        {
            AutoDepleteOnConsume = prefs.AutoDepleteOnConsume,
            DisplaySystem = prefs.DisplaySystem.ToString()
        });
    }
}
