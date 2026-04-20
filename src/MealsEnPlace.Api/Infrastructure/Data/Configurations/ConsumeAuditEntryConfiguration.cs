using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>Fluent API configuration for <see cref="ConsumeAuditEntry"/>.</summary>
public class ConsumeAuditEntryConfiguration : IEntityTypeConfiguration<ConsumeAuditEntry>
{
    public void Configure(EntityTypeBuilder<ConsumeAuditEntry> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DeductedQuantity)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.Property(a => a.OriginalLocation)
            .IsRequired()
            .HasConversion<string>();

        builder.HasIndex(a => a.MealPlanSlotId);

        builder.HasOne(a => a.CanonicalIngredient)
            .WithMany()
            .HasForeignKey(a => a.CanonicalIngredientId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.MealPlanSlot)
            .WithMany(s => s.ConsumeAuditEntries)
            .HasForeignKey(a => a.MealPlanSlotId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(a => a.UnitOfMeasureId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
