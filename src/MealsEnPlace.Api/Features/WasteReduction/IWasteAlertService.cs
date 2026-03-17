namespace MealsEnPlace.Api.Features.WasteReduction;

/// <summary>
/// Evaluates and manages waste reduction alerts for inventory items approaching expiry.
/// </summary>
public interface IWasteAlertService
{
    /// <summary>Dismisses an active alert.</summary>
    Task<bool> DismissAlertAsync(Guid alertId, CancellationToken cancellationToken = default);

    /// <summary>Scans inventory for expiry-imminent items, creates/updates alerts, and returns all active alerts.</summary>
    Task<List<WasteAlertResponse>> EvaluateAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all non-dismissed alerts without re-evaluating.</summary>
    Task<List<WasteAlertResponse>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
}
