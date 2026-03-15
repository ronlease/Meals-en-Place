using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="SeasonalityWindow"/>, including Zone 7a seed data.
/// Kale and Broccoli each have two windows (spring and autumn), stored as separate rows.
/// </summary>
public class SeasonalityWindowConfiguration : IEntityTypeConfiguration<SeasonalityWindow>
{
    public void Configure(EntityTypeBuilder<SeasonalityWindow> builder)
    {
        builder.HasKey(sw => sw.Id);

        builder.Property(sw => sw.PeakSeasonEnd)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(sw => sw.PeakSeasonStart)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(sw => sw.UsdaZone)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("7a");

        builder.HasOne(sw => sw.CanonicalIngredient)
            .WithMany(ci => ci.SeasonalityWindows)
            .HasForeignKey(sw => sw.CanonicalIngredientId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // ── Zone 7a seed data ─────────────────────────────────────────────────
        builder.HasData(
            // Tomatoes: June – September
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.TomatoesId,
                Id = new Guid("c1000000-0000-0000-0000-000000000001"),
                PeakSeasonEnd = Month.September,
                PeakSeasonStart = Month.June,
                UsdaZone = "7a"
            },
            // Corn: July – September
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.CornId,
                Id = new Guid("c1000000-0000-0000-0000-000000000002"),
                PeakSeasonEnd = Month.September,
                PeakSeasonStart = Month.July,
                UsdaZone = "7a"
            },
            // Zucchini: June – August
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.ZucchiniId,
                Id = new Guid("c1000000-0000-0000-0000-000000000003"),
                PeakSeasonEnd = Month.August,
                PeakSeasonStart = Month.June,
                UsdaZone = "7a"
            },
            // Strawberries: May – June
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.StrawberriesId,
                Id = new Guid("c1000000-0000-0000-0000-000000000004"),
                PeakSeasonEnd = Month.June,
                PeakSeasonStart = Month.May,
                UsdaZone = "7a"
            },
            // Apples: September – November
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.ApplesId,
                Id = new Guid("c1000000-0000-0000-0000-000000000005"),
                PeakSeasonEnd = Month.November,
                PeakSeasonStart = Month.September,
                UsdaZone = "7a"
            },
            // Kale window 1: March – May
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.KaleId,
                Id = new Guid("c1000000-0000-0000-0000-000000000006"),
                PeakSeasonEnd = Month.May,
                PeakSeasonStart = Month.March,
                UsdaZone = "7a"
            },
            // Kale window 2: September – November
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.KaleId,
                Id = new Guid("c1000000-0000-0000-0000-000000000007"),
                PeakSeasonEnd = Month.November,
                PeakSeasonStart = Month.September,
                UsdaZone = "7a"
            },
            // Asparagus: April – May
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.AsparagusId,
                Id = new Guid("c1000000-0000-0000-0000-000000000008"),
                PeakSeasonEnd = Month.May,
                PeakSeasonStart = Month.April,
                UsdaZone = "7a"
            },
            // Peaches: July – August
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.PeachesId,
                Id = new Guid("c1000000-0000-0000-0000-000000000009"),
                PeakSeasonEnd = Month.August,
                PeakSeasonStart = Month.July,
                UsdaZone = "7a"
            },
            // Pumpkin: September – October
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.PumpkinId,
                Id = new Guid("c1000000-0000-0000-0000-000000000010"),
                PeakSeasonEnd = Month.October,
                PeakSeasonStart = Month.September,
                UsdaZone = "7a"
            },
            // Broccoli window 1: April – May
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.BroccoliId,
                Id = new Guid("c1000000-0000-0000-0000-000000000011"),
                PeakSeasonEnd = Month.May,
                PeakSeasonStart = Month.April,
                UsdaZone = "7a"
            },
            // Broccoli window 2: September – October
            new SeasonalityWindow
            {
                CanonicalIngredientId = CanonicalIngredientConfiguration.BroccoliId,
                Id = new Guid("c1000000-0000-0000-0000-000000000012"),
                PeakSeasonEnd = Month.October,
                PeakSeasonStart = Month.September,
                UsdaZone = "7a"
            }
        );
    }
}
