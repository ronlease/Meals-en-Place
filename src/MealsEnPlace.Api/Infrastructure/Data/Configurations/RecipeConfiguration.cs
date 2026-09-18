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

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(300);

        // Supports ORDER BY Title on the paged list endpoint at 1.6 M-row scale
        // and keeps the parallel COUNT(*) affordable via an index-only scan.
        builder.HasIndex(r => r.Title)
            .HasDatabaseName("IX_Recipes_Title");

        // pg_trgm GIN index: accelerates ILIKE '%term%' title searches at 1.6 M-row scale.
        // Requires the pg_trgm extension (enabled in migration AddRecipeSearchTrigrams).
        builder.HasIndex([nameof(Recipe.Title)], "IX_Recipes_Title_Trgm")
            .HasAnnotation("Npgsql:IndexMethod", "gin")
            .HasAnnotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        // IsFullyResolved is computed — do not map to a column.
        builder.Ignore(r => r.IsFullyResolved);
    }
}
