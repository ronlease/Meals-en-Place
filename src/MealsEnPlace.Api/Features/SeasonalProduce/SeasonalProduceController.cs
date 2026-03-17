using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.SeasonalProduce;

/// <summary>
/// Seasonal produce guidance — what's in season for USDA Zone 7a.
/// </summary>
[ApiController]
[Route("api/v1/seasonal-produce")]
[Produces("application/json")]
public class SeasonalProduceController(ISeasonalProduceService seasonalProduceService) : ControllerBase
{
    /// <summary>Returns all seasonality windows.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with all seasonality windows across the full calendar.</returns>
    [HttpGet("all")]
    [ProducesResponseType(typeof(List<SeasonalProduceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SeasonalProduceResponse>>> GetAll(CancellationToken cancellationToken = default)
    {
        var windows = await seasonalProduceService.GetAllWindowsAsync(cancellationToken);
        return Ok(windows);
    }

    /// <summary>Returns produce items currently in season.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with produce currently in season for USDA Zone 7a.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<SeasonalProduceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SeasonalProduceResponse>>> GetInSeason(CancellationToken cancellationToken = default)
    {
        var produce = await seasonalProduceService.GetInSeasonAsync(cancellationToken);
        return Ok(produce);
    }
}
