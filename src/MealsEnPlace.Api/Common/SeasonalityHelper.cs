using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// Shared helper for seasonality date-range checks.
/// </summary>
public static class SeasonalityHelper
{
    /// <summary>
    /// Determines whether a given month falls within a seasonal window,
    /// handling wrap-around ranges (e.g., November–February).
    /// </summary>
    public static bool IsInSeason(Month currentMonth, Month start, Month end)
    {
        var current = (int)currentMonth;
        var s = (int)start;
        var e = (int)end;
        return s <= e ? current >= s && current <= e : current >= s || current <= e;
    }
}
