using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="InventoryItem"/>.
/// </summary>
public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.HasKey(ii => ii.Id);

        builder.Property(ii => ii.Location)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(ii => ii.Notes)
            .HasMaxLength(500);

        builder.Property(ii => ii.Quantity)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.HasOne(ii => ii.CanonicalIngredient)
            .WithMany(ci => ci.InventoryItems)
            .HasForeignKey(ii => ii.CanonicalIngredientId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ii => ii.Uom)
            .WithMany()
            .HasForeignKey(ii => ii.UomId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
