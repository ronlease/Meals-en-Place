using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="UserPreferences"/>.
/// A check constraint enforces that the table contains at most one row.
/// A default row with <see cref="DisplaySystem.Imperial"/> is seeded.
/// </summary>
public class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public static readonly Guid DefaultPreferencesId = new("d1000000-0000-0000-0000-000000000001");

    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.HasKey(up => up.Id);

        builder.Property(up => up.DisplaySystem)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(DisplaySystem.Imperial);

        // Enforce single-row constraint: Id must be the fixed seed Guid.
        builder.ToTable(tb =>
            tb.HasCheckConstraint(
                "CK_UserPreferences_SingleRow",
                $"\"Id\" = '{DefaultPreferencesId}'"));

        // ── Seed default preferences ──────────────────────────────────────────
        builder.HasData(new UserPreferences
        {
            DisplaySystem = DisplaySystem.Imperial,
            Id = DefaultPreferencesId
        });
    }
}
