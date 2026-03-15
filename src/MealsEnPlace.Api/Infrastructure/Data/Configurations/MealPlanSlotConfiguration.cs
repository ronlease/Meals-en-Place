using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="MealPlanSlot"/>.
/// </summary>
public class MealPlanSlotConfiguration : IEntityTypeConfiguration<MealPlanSlot>
{
    public void Configure(EntityTypeBuilder<MealPlanSlot> builder)
    {
        builder.HasKey(mps => mps.Id);

        builder.Property(mps => mps.DayOfWeek)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(mps => mps.MealSlot)
            .IsRequired()
            .HasConversion<string>();

        builder.HasIndex(mps => new { mps.MealPlanId, mps.DayOfWeek, mps.MealSlot })
            .IsUnique();

        builder.HasOne(mps => mps.MealPlan)
            .WithMany(mp => mp.Slots)
            .HasForeignKey(mps => mps.MealPlanId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mps => mps.Recipe)
            .WithMany(r => r.MealPlanSlots)
            .HasForeignKey(mps => mps.RecipeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
