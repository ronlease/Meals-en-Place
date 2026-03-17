using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.WasteReduction;

/// <summary>
/// Waste reduction alerts — surfaces expiry-imminent inventory items with matching recipes.
/// </summary>
[ApiController]
[Route("api/v1/waste-alerts")]
[Produces("application/json")]
public class WasteAlertController(IWasteAlertService wasteAlertService) : ControllerBase
{
    /// <summary>Dismisses an active waste alert.</summary>
    [HttpPost("{id:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DismissAlert(Guid id, CancellationToken cancellationToken = default)
    {
        var dismissed = await wasteAlertService.DismissAlertAsync(id, cancellationToken);
        return dismissed ? NoContent() : NotFound();
    }

    /// <summary>Evaluates inventory for expiry-imminent items and returns active waste alerts with suggested recipes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<WasteAlertResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WasteAlertResponse>>> GetAlerts(CancellationToken cancellationToken = default)
    {
        var alerts = await wasteAlertService.EvaluateAlertsAsync(cancellationToken);
        return Ok(alerts);
    }
}
