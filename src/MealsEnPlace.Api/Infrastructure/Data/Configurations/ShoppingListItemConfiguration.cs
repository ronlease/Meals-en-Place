using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealsEnPlace.Api.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for <see cref="ShoppingListItem"/>.
/// </summary>
public class ShoppingListItemConfiguration : IEntityTypeConfiguration<ShoppingListItem>
{
    public void Configure(EntityTypeBuilder<ShoppingListItem> builder)
    {
        builder.HasKey(sli => sli.Id);

        builder.Property(sli => sli.Notes)
            .HasMaxLength(500);

        builder.Property(sli => sli.Quantity)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.HasOne(sli => sli.CanonicalIngredient)
            .WithMany(ci => ci.ShoppingListItems)
            .HasForeignKey(sli => sli.CanonicalIngredientId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sli => sli.MealPlan)
            .WithMany()
            .HasForeignKey(sli => sli.MealPlanId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sli => sli.Uom)
            .WithMany()
            .HasForeignKey(sli => sli.UomId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
