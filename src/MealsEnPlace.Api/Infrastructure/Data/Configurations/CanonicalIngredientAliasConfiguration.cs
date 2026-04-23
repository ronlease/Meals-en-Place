using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="CanonicalIngredientAlias"/>. Written by the
/// MEP-038 dedup tool when it folds noisier canonical rows into a generic survivor.
/// </summary>
public class CanonicalIngredientAliasConfiguration : IEntityTypeConfiguration<CanonicalIngredientAlias>
{
    public void Configure(EntityTypeBuilder<CanonicalIngredientAlias> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Alias)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.CanonicalIngredientId)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasOne(a => a.CanonicalIngredient)
            .WithMany(ci => ci.Aliases)
            .HasForeignKey(a => a.CanonicalIngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Non-unique index to support case-insensitive lookup of folded names.
        // No DB-level uniqueness: two different losers can share a normalized
        // key yet hash to the same alias text (rare, but observed in Kaggle
        // noise like trailing whitespace variants), and a hard unique
        // constraint would reject legitimate second-pass folds.
        builder.HasIndex(a => a.Alias);
    }
}
