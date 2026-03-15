namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Calendar month, used to define seasonality windows.
/// Integer values match the .NET <see cref="System.DateTime.Month"/> convention (1 = January).
/// </summary>
public enum Month
{
    /// <summary>January (month 1).</summary>
    January = 1,

    /// <summary>February (month 2).</summary>
    February = 2,

    /// <summary>March (month 3).</summary>
    March = 3,

    /// <summary>April (month 4).</summary>
    April = 4,

    /// <summary>May (month 5).</summary>
    May = 5,

    /// <summary>June (month 6).</summary>
    June = 6,

    /// <summary>July (month 7).</summary>
    July = 7,

    /// <summary>August (month 8).</summary>
    August = 8,

    /// <summary>September (month 9).</summary>
    September = 9,

    /// <summary>October (month 10).</summary>
    October = 10,

    /// <summary>November (month 11).</summary>
    November = 11,

    /// <summary>December (month 12).</summary>
    December = 12
}
