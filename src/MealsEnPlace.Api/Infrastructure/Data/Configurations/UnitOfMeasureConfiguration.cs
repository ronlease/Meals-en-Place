using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="UnitOfMeasure"/>, including conversion table seed data.
/// ConversionFactor converts FROM the named unit TO the base unit.
/// Base units have BaseUomId = null and ConversionFactor = 1.0.
/// </summary>
public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    // ── Base unit IDs ─────────────────────────────────────────────────────────
    public static readonly Guid EachId   = new("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid GramId   = new("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid MlId     = new("a1000000-0000-0000-0000-000000000003");

    // ── Volume unit IDs ───────────────────────────────────────────────────────
    public static readonly Guid CupId    = new("a1000000-0000-0000-0000-000000000004");
    public static readonly Guid FlOzId   = new("a1000000-0000-0000-0000-000000000005");
    public static readonly Guid LiterId  = new("a1000000-0000-0000-0000-000000000006");
    public static readonly Guid PintId   = new("a1000000-0000-0000-0000-000000000007");
    public static readonly Guid QuartId  = new("a1000000-0000-0000-0000-000000000008");
    public static readonly Guid TbspId   = new("a1000000-0000-0000-0000-000000000009");
    public static readonly Guid TspId    = new("a1000000-0000-0000-0000-000000000010");

    // ── Weight unit IDs ───────────────────────────────────────────────────────
    public static readonly Guid KgId     = new("a1000000-0000-0000-0000-000000000011");
    public static readonly Guid LbId     = new("a1000000-0000-0000-0000-000000000012");
    public static readonly Guid OzId     = new("a1000000-0000-0000-0000-000000000013");

    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Abbreviation)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.ConversionFactor)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.UomType)
            .IsRequired()
            .HasConversion<string>();

        builder.HasOne(u => u.BaseUom)
            .WithMany(u => u.DerivedUnits)
            .HasForeignKey(u => u.BaseUomId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.Abbreviation).IsUnique();

        // ── Seed data ─────────────────────────────────────────────────────────

        // Base units
        builder.HasData(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = EachId,
                Name = "Each",
                UomType = UomType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "g",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = GramId,
                Name = "Gram",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "ml",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = MlId,
                Name = "Milliliter",
                UomType = UomType.Volume
            }
        );

        // Volume units (base = ml)
        builder.HasData(
            new UnitOfMeasure
            {
                Abbreviation = "cup",
                BaseUomId = MlId,
                ConversionFactor = 236.588m,
                Id = CupId,
                Name = "Cup",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "fl oz",
                BaseUomId = MlId,
                ConversionFactor = 29.574m,
                Id = FlOzId,
                Name = "Fluid Ounce",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "L",
                BaseUomId = MlId,
                ConversionFactor = 1000.0m,
                Id = LiterId,
                Name = "Liter",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "pt",
                BaseUomId = MlId,
                ConversionFactor = 473.176m,
                Id = PintId,
                Name = "Pint",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "qt",
                BaseUomId = MlId,
                ConversionFactor = 946.353m,
                Id = QuartId,
                Name = "Quart",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tbsp",
                BaseUomId = MlId,
                ConversionFactor = 14.787m,
                Id = TbspId,
                Name = "Tablespoon",
                UomType = UomType.Volume
            },
            new UnitOfMeasure
            {
                Abbreviation = "tsp",
                BaseUomId = MlId,
                ConversionFactor = 4.929m,
                Id = TspId,
                Name = "Teaspoon",
                UomType = UomType.Volume
            }
        );

        // Weight units (base = g)
        builder.HasData(
            new UnitOfMeasure
            {
                Abbreviation = "kg",
                BaseUomId = GramId,
                ConversionFactor = 1000.0m,
                Id = KgId,
                Name = "Kilogram",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "lb",
                BaseUomId = GramId,
                ConversionFactor = 453.592m,
                Id = LbId,
                Name = "Pound",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "oz",
                BaseUomId = GramId,
                ConversionFactor = 28.350m,
                Id = OzId,
                Name = "Ounce",
                UomType = UomType.Weight
            }
        );
    }
}
