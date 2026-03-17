using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.SeasonalProduce;

/// <summary>
/// Queries seasonality windows from the database, filtered by current month for in-season results.
/// </summary>
public class SeasonalProduceService(MealsEnPlaceDbContext dbContext) : ISeasonalProduceService
{
    /// <inheritdoc />
    public async Task<List<SeasonalProduceResponse>> GetAllWindowsAsync(CancellationToken cancellationToken = default)
    {
        var windows = await dbContext.SeasonalityWindows
            .AsNoTracking()
            .Include(sw => sw.CanonicalIngredient)
            .OrderBy(sw => sw.CanonicalIngredient.Name)
            .ThenBy(sw => sw.PeakSeasonStart)
            .ToListAsync(cancellationToken);

        return windows.Select(MapToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<List<SeasonalProduceResponse>> GetInSeasonAsync(CancellationToken cancellationToken = default)
    {
        var currentMonth = (Month)DateTime.UtcNow.Month;

        var windows = await dbContext.SeasonalityWindows
            .AsNoTracking()
            .Include(sw => sw.CanonicalIngredient)
            .ToListAsync(cancellationToken);

        return windows
            .Where(sw => IsInSeason(currentMonth, sw.PeakSeasonStart, sw.PeakSeasonEnd))
            .OrderBy(sw => sw.CanonicalIngredient.Name)
            .Select(MapToResponse)
            .ToList();
    }

    private static bool IsInSeason(Month currentMonth, Month start, Month end)
    {
        var current = (int)currentMonth;
        var s = (int)start;
        var e = (int)end;
        return s <= e ? current >= s && current <= e : current >= s || current <= e;
    }

    private static SeasonalProduceResponse MapToResponse(SeasonalityWindow sw)
    {
        return new SeasonalProduceResponse
        {
            IngredientId = sw.CanonicalIngredientId,
            Name = sw.CanonicalIngredient.Name,
            PeakSeasonEnd = sw.PeakSeasonEnd,
            PeakSeasonStart = sw.PeakSeasonStart,
            UsdaZone = sw.UsdaZone
        };
    }
}
