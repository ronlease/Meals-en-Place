namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Single-row table holding application-wide user preferences.
/// A check constraint in the Fluent API ensures at most one row exists.
/// The <see cref="DisplaySystem"/> field is stubbed at MVP — no UI toggle exists yet,
/// but the column is present to avoid a future breaking migration.
/// </summary>
public class UserPreferences
{
    /// <summary>
    /// Controls how quantities render at the API response layer.
    /// Default: <see cref="Models.Entities.DisplaySystem.Imperial"/>.
    /// </summary>
    public DisplaySystem DisplaySystem { get; set; } = DisplaySystem.Imperial;

    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }
}
