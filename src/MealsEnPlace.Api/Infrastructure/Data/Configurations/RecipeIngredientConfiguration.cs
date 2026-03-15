using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="RecipeIngredient"/>.
/// </summary>
public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.HasKey(ri => ri.Id);

        builder.Property(ri => ri.IsContainerResolved)
            .IsRequired();

        builder.Property(ri => ri.Notes)
            .HasMaxLength(500);

        builder.Property(ri => ri.Quantity)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.HasOne(ri => ri.CanonicalIngredient)
            .WithMany(ci => ci.RecipeIngredients)
            .HasForeignKey(ri => ri.CanonicalIngredientId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ri => ri.Recipe)
            .WithMany(r => r.RecipeIngredients)
            .HasForeignKey(ri => ri.RecipeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ri => ri.Uom)
            .WithMany()
            .HasForeignKey(ri => ri.UomId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
