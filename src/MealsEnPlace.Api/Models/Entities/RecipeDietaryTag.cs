namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Many-to-many join between a <see cref="Recipe"/> and a <see cref="DietaryTag"/>.
/// </summary>
public class RecipeDietaryTag
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The recipe this tag applies to.</summary>
    public Guid RecipeId { get; set; }

    /// <summary>The dietary tag assigned to the recipe.</summary>
    public DietaryTag Tag { get; set; }

    // Navigation properties

    /// <summary>The recipe this tag applies to.</summary>
    public Recipe Recipe { get; set; } = null!;
}
