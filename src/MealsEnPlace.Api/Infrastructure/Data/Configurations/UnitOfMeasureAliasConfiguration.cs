using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="UnitOfMeasureAlias"/>, including the seeded
/// set of common variants observed in recipe datasets (dotted abbreviations, plural
/// forms, and alternate short-forms).
/// </summary>
public class UnitOfMeasureAliasConfiguration : IEntityTypeConfiguration<UnitOfMeasureAlias>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasureAlias> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Alias)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UnitOfMeasureId)
            .IsRequired();

        builder.HasOne(a => a.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(a => a.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.Alias).IsUnique();

        // ── Seed data ─────────────────────────────────────────────────────────
        //
        // Common variants observed in recipe datasets (MEP-025 spike). Alias
        // values are case-insensitive at match time; the stored form is what
        // appears in the dataset so it is easy to debug from the seed list.

        var seededAt = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            // Cup
            NewAlias("a2000000-0000-0000-0000-000000000001", "c",
                UnitOfMeasureConfiguration.CupId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000002", "c.",
                UnitOfMeasureConfiguration.CupId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000003", "cups",
                UnitOfMeasureConfiguration.CupId, seededAt),

            // Tablespoon
            NewAlias("a2000000-0000-0000-0000-000000000010", "T",
                UnitOfMeasureConfiguration.TbspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000011", "T.",
                UnitOfMeasureConfiguration.TbspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000012", "Tbs",
                UnitOfMeasureConfiguration.TbspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000013", "Tbs.",
                UnitOfMeasureConfiguration.TbspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000014", "Tbl",
                UnitOfMeasureConfiguration.TbspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000015", "Tbsp.",
                UnitOfMeasureConfiguration.TbspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000016", "tbsps",
                UnitOfMeasureConfiguration.TbspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000017", "tablespoons",
                UnitOfMeasureConfiguration.TbspId, seededAt),

            // Teaspoon
            NewAlias("a2000000-0000-0000-0000-000000000020", "t",
                UnitOfMeasureConfiguration.TspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000021", "t.",
                UnitOfMeasureConfiguration.TspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000022", "tsp.",
                UnitOfMeasureConfiguration.TspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000023", "tsps",
                UnitOfMeasureConfiguration.TspId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000024", "teaspoons",
                UnitOfMeasureConfiguration.TspId, seededAt),

            // Ounce (weight)
            NewAlias("a2000000-0000-0000-0000-000000000030", "oz.",
                UnitOfMeasureConfiguration.OzId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000031", "ozs",
                UnitOfMeasureConfiguration.OzId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000032", "ozs.",
                UnitOfMeasureConfiguration.OzId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000033", "ounces",
                UnitOfMeasureConfiguration.OzId, seededAt),

            // Fluid Ounce
            NewAlias("a2000000-0000-0000-0000-000000000040", "fl. oz",
                UnitOfMeasureConfiguration.FlOzId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000041", "fl. oz.",
                UnitOfMeasureConfiguration.FlOzId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000042", "fluid oz",
                UnitOfMeasureConfiguration.FlOzId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000043", "fluid ounces",
                UnitOfMeasureConfiguration.FlOzId, seededAt),

            // Pound
            NewAlias("a2000000-0000-0000-0000-000000000050", "lb.",
                UnitOfMeasureConfiguration.LbId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000051", "lbs",
                UnitOfMeasureConfiguration.LbId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000052", "lbs.",
                UnitOfMeasureConfiguration.LbId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000053", "pounds",
                UnitOfMeasureConfiguration.LbId, seededAt),

            // Gram
            NewAlias("a2000000-0000-0000-0000-000000000060", "g.",
                UnitOfMeasureConfiguration.GramId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000061", "gm",
                UnitOfMeasureConfiguration.GramId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000062", "gms",
                UnitOfMeasureConfiguration.GramId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000063", "grams",
                UnitOfMeasureConfiguration.GramId, seededAt),

            // Kilogram
            NewAlias("a2000000-0000-0000-0000-000000000070", "kg.",
                UnitOfMeasureConfiguration.KgId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000071", "kgs",
                UnitOfMeasureConfiguration.KgId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000072", "kilograms",
                UnitOfMeasureConfiguration.KgId, seededAt),

            // Milliliter
            NewAlias("a2000000-0000-0000-0000-000000000080", "ml.",
                UnitOfMeasureConfiguration.MlId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000081", "mls",
                UnitOfMeasureConfiguration.MlId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000082", "milliliters",
                UnitOfMeasureConfiguration.MlId, seededAt),

            // Liter
            NewAlias("a2000000-0000-0000-0000-000000000090", "l",
                UnitOfMeasureConfiguration.LiterId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000091", "l.",
                UnitOfMeasureConfiguration.LiterId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000092", "liters",
                UnitOfMeasureConfiguration.LiterId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000093", "litres",
                UnitOfMeasureConfiguration.LiterId, seededAt),

            // Pint
            NewAlias("a2000000-0000-0000-0000-000000000100", "pt.",
                UnitOfMeasureConfiguration.PintId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000101", "pts",
                UnitOfMeasureConfiguration.PintId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000102", "pints",
                UnitOfMeasureConfiguration.PintId, seededAt),

            // Quart
            NewAlias("a2000000-0000-0000-0000-000000000110", "qt.",
                UnitOfMeasureConfiguration.QuartId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000111", "qts",
                UnitOfMeasureConfiguration.QuartId, seededAt),
            NewAlias("a2000000-0000-0000-0000-000000000112", "quarts",
                UnitOfMeasureConfiguration.QuartId, seededAt),

            // Each
            NewAlias("a2000000-0000-0000-0000-000000000120", "each",
                UnitOfMeasureConfiguration.EachId, seededAt)
        );
    }

    private static UnitOfMeasureAlias NewAlias(string id, string alias, Guid uomId, DateTime seededAt) =>
        new()
        {
            Alias = alias,
            CreatedAt = seededAt,
            Id = new Guid(id),
            UnitOfMeasureId = uomId
        };
}
