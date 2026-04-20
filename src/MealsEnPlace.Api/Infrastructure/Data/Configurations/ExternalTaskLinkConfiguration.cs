using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>Fluent API configuration for <see cref="ExternalTaskLink"/>.</summary>
public class ExternalTaskLinkConfiguration : IEntityTypeConfiguration<ExternalTaskLink>
{
    public void Configure(EntityTypeBuilder<ExternalTaskLink> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(l => l.ExternalProjectId)
            .HasMaxLength(100);

        builder.Property(l => l.ExternalTaskId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.SourceScope)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.SourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Lookup path used by every push: given a source row + provider, is
        // there an existing link? The uniqueness also guarantees we never
        // double-record a push for the same (source, provider) pair.
        builder.HasIndex(l => new { l.SourceType, l.SourceId, l.Provider })
            .IsUnique();

        // Scope-scoped enumeration: given a push scope, list every link that
        // *should* have a matching source row. Any link in the scope whose
        // SourceId is no longer present is an orphan to close.
        builder.HasIndex(l => new { l.Provider, l.SourceType, l.SourceScope });

        // MEP-036 will enumerate distinct project IDs per provider — precompute
        // the index so that query stays cheap as the history grows.
        builder.HasIndex(l => new { l.Provider, l.ExternalProjectId });
    }
}
