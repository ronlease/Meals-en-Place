// Feature: Recipe Dietary Classification (Rule-Based Stub)
//
// Scenario: Classify a vegetarian recipe
//   Given a recipe contains no meat, poultry, or fish ingredients
//   When Claude analyzes the recipe
//   Then the recipe is tagged "Vegetarian"
//
// Scenario: Classify a vegan recipe
//   Given a recipe contains no animal products of any kind
//   When Claude analyzes the recipe
//   Then the recipe is tagged "Vegan", "Vegetarian", and "DairyFree"
//
// Scenario: Classify a carnivore recipe
//   Given a recipe is primarily composed of meat
//   When Claude analyzes the recipe
//   Then the recipe is tagged "Carnivore"
//
// Scenario: GlutenFree classification
//   Given a recipe uses no gluten-containing grains
//   When Claude analyzes the recipe
//   Then the recipe is tagged "GlutenFree"
//
// Scenario: Recipe with meat and dairy
//   Given a recipe uses chicken and cheese
//   When Claude analyzes the recipe
//   Then it is tagged "Carnivore" but not "Vegetarian", "Vegan", or "DairyFree"

using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Unit.Infrastructure.Claude;

public class DietaryClassificationTests
{
    private readonly ClaudeService _sut = new();

    private static Recipe BuildRecipe(params string[] ingredientNames)
    {
        var recipe = new Recipe
        {
            CuisineType = "Test",
            Id = Guid.NewGuid(),
            Instructions = "Cook it.",
            ServingCount = 4,
            Title = "Test Recipe"
        };

        foreach (var name in ingredientNames)
        {
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredient = new CanonicalIngredient
                {
                    Category = IngredientCategory.Other,
                    Id = Guid.NewGuid(),
                    Name = name
                },
                CanonicalIngredientId = Guid.NewGuid(),
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = 1m,
                RecipeId = recipe.Id
            });
        }

        return recipe;
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_VegetarianRecipe_TaggedVegetarian()
    {
        var recipe = BuildRecipe("Tofu", "Rice", "Soy Sauce", "Ginger");
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().Contain(DietaryTag.Vegetarian);
        tags.Should().NotContain(DietaryTag.Carnivore);
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_VeganRecipe_TaggedVeganVegetarianDairyFree()
    {
        var recipe = BuildRecipe("Tofu", "Rice", "Olive Oil", "Garlic");
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().Contain(DietaryTag.Vegan);
        tags.Should().Contain(DietaryTag.Vegetarian);
        tags.Should().Contain(DietaryTag.DairyFree);
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_CarnivoreRecipe_TaggedCarnivore()
    {
        var recipe = BuildRecipe("Chicken Breast", "Salt", "Pepper");
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().Contain(DietaryTag.Carnivore);
        tags.Should().NotContain(DietaryTag.Vegetarian);
        tags.Should().NotContain(DietaryTag.Vegan);
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_GlutenFreeRecipe_TaggedGlutenFree()
    {
        var recipe = BuildRecipe("Rice", "Chicken Breast", "Salt");
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().Contain(DietaryTag.GlutenFree);
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_RecipeWithGluten_NotTaggedGlutenFree()
    {
        var recipe = BuildRecipe("All-Purpose Flour", "Butter", "Sugar");
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().NotContain(DietaryTag.GlutenFree);
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_MeatAndDairy_CarnivoreNotVegetarianNotDairyFree()
    {
        var recipe = BuildRecipe("Chicken Breast", "Cheddar Cheese", "Olive Oil");
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().Contain(DietaryTag.Carnivore);
        tags.Should().NotContain(DietaryTag.Vegetarian);
        tags.Should().NotContain(DietaryTag.Vegan);
        tags.Should().NotContain(DietaryTag.DairyFree);
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_EmptyIngredients_ReturnsEmptyTags()
    {
        var recipe = new Recipe
        {
            CuisineType = "Test",
            Id = Guid.NewGuid(),
            Instructions = "Nothing.",
            ServingCount = 1,
            Title = "Empty"
        };
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ClassifyDietaryTagsAsync_VegetarianWithDairy_NotVegan()
    {
        var recipe = BuildRecipe("Mozzarella Cheese", "Tomatoes", "Basil");
        var tags = await _sut.ClassifyDietaryTagsAsync(recipe);
        tags.Should().Contain(DietaryTag.Vegetarian);
        tags.Should().NotContain(DietaryTag.Vegan);
        tags.Should().NotContain(DietaryTag.DairyFree);
    }
}
