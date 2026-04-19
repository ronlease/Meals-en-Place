using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="UnresolvedUomToken"/>.
/// Uniqueness on <see cref="UnresolvedUomToken.UnitToken"/> is enforced at the
/// service layer (upsert semantics), not by a DB-level unique index, to mirror
/// the case-sensitive-by-default stance used for <see cref="UnitOfMeasureAlias"/>.
/// </summary>
public class UnresolvedUomTokenConfiguration : IEntityTypeConfiguration<UnresolvedUomToken>
{
    public void Configure(EntityTypeBuilder<UnresolvedUomToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Count)
            .IsRequired();

        builder.Property(t => t.FirstSeenAt)
            .IsRequired();

        builder.Property(t => t.LastSeenAt)
            .IsRequired();

        builder.Property(t => t.SampleIngredientContext)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.SampleMeasureString)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.UnitToken)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.UnitToken);
    }
}
