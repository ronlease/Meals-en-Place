using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="Recipe"/>.
/// <see cref="Recipe.IsFullyResolved"/> is a computed property and is not mapped to a column.
/// </summary>
public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.CuisineType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Instructions)
            .IsRequired();

        builder.Property(r => r.ServingCount)
            .IsRequired();

        builder.Property(r => r.SourceUrl)
            .HasMaxLength(2000);

        builder.Property(r => r.TheMealDbId)
            .HasMaxLength(50);

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.HasIndex(r => r.TheMealDbId)
            .IsUnique()
            .HasFilter("\"TheMealDbId\" IS NOT NULL");

        // IsFullyResolved is computed — do not map to a column.
        builder.Ignore(r => r.IsFullyResolved);
    }
}
