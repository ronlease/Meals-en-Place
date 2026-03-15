using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="MealPlan"/>.
/// </summary>
public class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
{
    public void Configure(EntityTypeBuilder<MealPlan> builder)
    {
        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.CreatedAt)
            .IsRequired();

        builder.Property(mp => mp.Name)
            .IsRequired()
            .HasMaxLength(200);
    }
}
