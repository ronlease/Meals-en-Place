using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="RecipeDietaryTag"/>.
/// </summary>
public class RecipeDietaryTagConfiguration : IEntityTypeConfiguration<RecipeDietaryTag>
{
    public void Configure(EntityTypeBuilder<RecipeDietaryTag> builder)
    {
        builder.HasKey(rdt => rdt.Id);

        builder.Property(rdt => rdt.Tag)
            .IsRequired()
            .HasConversion<string>();

        builder.HasIndex(rdt => new { rdt.RecipeId, rdt.Tag }).IsUnique();

        builder.HasOne(rdt => rdt.Recipe)
            .WithMany(r => r.DietaryTags)
            .HasForeignKey(rdt => rdt.RecipeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
