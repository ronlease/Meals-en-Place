using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="CanonicalIngredient"/>.
/// Seed data covers the produce items required by the Zone 7a seasonality windows.
/// </summary>
public class CanonicalIngredientConfiguration : IEntityTypeConfiguration<CanonicalIngredient>
{
    // ── Seed ingredient IDs (produce required for seasonality windows) ─────────
    public static readonly Guid ApplesId      = new("b1000000-0000-0000-0000-000000000001");
    public static readonly Guid AsparagusId   = new("b1000000-0000-0000-0000-000000000002");
    public static readonly Guid BroccoliId    = new("b1000000-0000-0000-0000-000000000003");
    public static readonly Guid CornId        = new("b1000000-0000-0000-0000-000000000004");
    public static readonly Guid KaleId        = new("b1000000-0000-0000-0000-000000000005");
    public static readonly Guid PeachesId     = new("b1000000-0000-0000-0000-000000000006");
    public static readonly Guid PumpkinId     = new("b1000000-0000-0000-0000-000000000007");
    public static readonly Guid StrawberriesId = new("b1000000-0000-0000-0000-000000000008");
    public static readonly Guid TomatoesId    = new("b1000000-0000-0000-0000-000000000009");
    public static readonly Guid ZucchiniId    = new("b1000000-0000-0000-0000-000000000010");

    public void Configure(EntityTypeBuilder<CanonicalIngredient> builder)
    {
        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Category)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(ci => ci.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(ci => ci.Name).IsUnique();

        builder.HasOne(ci => ci.DefaultUom)
            .WithMany()
            .HasForeignKey(ci => ci.DefaultUomId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // ── Seed: Zone 7a produce ─────────────────────────────────────────────
        builder.HasData(
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = ApplesId,
                Name = "Apples"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = AsparagusId,
                Name = "Asparagus"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = BroccoliId,
                Name = "Broccoli"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = CornId,
                Name = "Corn"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = KaleId,
                Name = "Kale"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = PeachesId,
                Name = "Peaches"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = PumpkinId,
                Name = "Pumpkin"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = StrawberriesId,
                Name = "Strawberries"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = TomatoesId,
                Name = "Tomatoes"
            },
            new CanonicalIngredient
            {
                Category = IngredientCategory.Produce,
                DefaultUomId = UnitOfMeasureConfiguration.EachId,
                Id = ZucchiniId,
                Name = "Zucchini"
            }
        );
    }
}
