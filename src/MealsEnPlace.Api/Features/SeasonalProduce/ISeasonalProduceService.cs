namespace MealsEnPlace.Api.Features.SeasonalProduce;

/// <summary>
/// Queries seasonal produce data for the user's USDA zone.
/// </summary>
public interface ISeasonalProduceService
{
    /// <summary>Returns all produce items currently in season.</summary>
    Task<List<SeasonalProduceResponse>> GetInSeasonAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all seasonality windows regardless of current date.</summary>
    Task<List<SeasonalProduceResponse>> GetAllWindowsAsync(CancellationToken cancellationToken = default);
}
