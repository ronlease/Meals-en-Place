using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="WasteAlert"/>.
/// <see cref="WasteAlert.MatchedRecipeIds"/> is stored as a PostgreSQL JSON column.
/// </summary>
public class WasteAlertConfiguration : IEntityTypeConfiguration<WasteAlert>
{
    public void Configure(EntityTypeBuilder<WasteAlert> builder)
    {
        builder.HasKey(wa => wa.Id);

        builder.Property(wa => wa.CreatedAt)
            .IsRequired();

        builder.Property(wa => wa.ExpiryDate)
            .IsRequired();

        builder.Property(wa => wa.MatchedRecipeIds)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasOne(wa => wa.InventoryItem)
            .WithMany(ii => ii.WasteAlerts)
            .HasForeignKey(wa => wa.InventoryItemId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
