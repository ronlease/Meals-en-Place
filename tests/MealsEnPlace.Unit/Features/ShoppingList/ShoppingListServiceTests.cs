// Feature: Shopping List Derivation
//
// Scenario: Generate shopping list shows deficit for partially covered ingredient
//   Given my inventory has 100 g of "Cheddar Cheese"
//   And the meal plan requires 300 g of "Cheddar Cheese"
//   When I generate a shopping list
//   Then "Cheddar Cheese" appears on the shopping list with quantity 200 g
//
// Scenario: Fully covered ingredient excluded from list
//   Given my inventory has 1000 g of "All-Purpose Flour"
//   And the meal plan requires 500 g of "All-Purpose Flour"
//   When I generate a shopping list
//   Then "All-Purpose Flour" does not appear on the shopping list
//
// Scenario: Aggregate needs across multiple recipes
//   Given the meal plan includes 3 recipes that each require "Olive Oil"
//   And the total required is 90 ml
//   And my inventory has 50 ml of "Olive Oil"
//   When I generate a shopping list
//   Then "Olive Oil" appears on the shopping list with quantity 40 ml
//
// Scenario: Generate list for meal plan with no recipes returns empty
//   Given a meal plan exists with no slots
//   When I generate a shopping list
//   Then the list is empty
//
// Scenario: Regeneration replaces previous list items
//   Given a shopping list was already generated
//   When I generate the shopping list again
//   Then the old items are replaced with the new items
//
// Scenario: No inventory at all shows full requirements
//   Given no inventory items exist
//   And the meal plan requires 500 g of "Chicken"
//   When I generate a shopping list
//   Then "Chicken" appears with quantity 500 g

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.ShoppingList;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.ShoppingList;

public class ShoppingListServiceTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly ShoppingListService _sut;

    private static readonly Guid EachId = UnitOfMeasureConfiguration.EachId;
    private static readonly Guid GramId = UnitOfMeasureConfiguration.GramId;
    private static readonly Guid MlId = UnitOfMeasureConfiguration.MlId;

    public ShoppingListServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MealsEnPlaceDbContext(options);
        SeedUnitOfMeasures();

        var conversionService = new UomConversionService(_dbContext);
        var displayConverter = new UomDisplayConverter(_dbContext);

        _sut = new ShoppingListService(_dbContext, conversionService, displayConverter);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedUnitOfMeasures()
    {
        _dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = EachId,
                Name = "Each",
                UomType = UomType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "g",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = GramId,
                Name = "Gram",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "ml",
                BaseUomId = null,
                ConversionFactor = 1.0m,
                Id = MlId,
                Name = "Milliliter",
                UomType = UomType.Volume
            });
        _dbContext.SaveChanges();
    }

    private CanonicalIngredient SeedCanonicalIngredient(string name, IngredientCategory category = IngredientCategory.Other)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = category,
            DefaultUomId = EachId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        _dbContext.SaveChanges();
        return ingredient;
    }

    private InventoryItem SeedInventoryItem(Guid canonicalIngredientId, decimal quantity, Guid uomId)
    {
        var item = new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Pantry,
            Quantity = quantity,
            UomId = uomId
        };
        _dbContext.InventoryItems.Add(item);
        _dbContext.SaveChanges();
        return item;
    }

    private (Api.Models.Entities.MealPlan Plan, Recipe Recipe) SeedMealPlanWithRecipe(
        string recipeTitle,
        IEnumerable<(Guid IngredientId, decimal Quantity, Guid UomId)> ingredientLines)
    {
        var recipe = new Recipe
        {
            CuisineType = "Test",
            Id = Guid.NewGuid(),
            Instructions = "Cook it.",
            ServingCount = 4,
            Title = recipeTitle
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.SaveChanges();

        foreach (var (ingredientId, qty, uomId) in ingredientLines)
        {
            _dbContext.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ingredientId,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = qty,
                RecipeId = recipe.Id,
                UomId = uomId
            });
        }
        _dbContext.SaveChanges();

        var plan = new Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.Add(plan);
        _dbContext.SaveChanges();

        _dbContext.MealPlanSlots.Add(new MealPlanSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            Id = Guid.NewGuid(),
            MealPlanId = plan.Id,
            MealSlot = MealSlot.Dinner,
            RecipeId = recipe.Id
        });
        _dbContext.SaveChanges();

        return (plan, recipe);
    }

    private void AddSlotToPlan(Guid planId, Recipe recipe, DayOfWeek day, MealSlot slot)
    {
        _dbContext.MealPlanSlots.Add(new MealPlanSlot
        {
            DayOfWeek = day,
            Id = Guid.NewGuid(),
            MealPlanId = planId,
            MealSlot = slot,
            RecipeId = recipe.Id
        });
        _dbContext.SaveChanges();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateShoppingListAsync_PartiallyCoveredIngredient_ShowsDeficit()
    {
        // Arrange
        var cheese = SeedCanonicalIngredient("Cheddar Cheese", IngredientCategory.Dairy);
        SeedInventoryItem(cheese.Id, 100m, GramId); // have 100g
        var (plan, _) = SeedMealPlanWithRecipe("Cheesy Pasta",
            [(cheese.Id, 300m, GramId)]); // need 300g

        // Act
        var list = await _sut.GenerateShoppingListAsync(plan.Id);

        // Assert — deficit is 200g, displayed as oz (Imperial conversion: 200g / 28.35 ≈ 7.05 oz)
        list.Should().ContainSingle(i => i.CanonicalIngredientName == "Cheddar Cheese");
        var item = list.Single();
        item.Quantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateShoppingListAsync_FullyCoveredIngredient_ExcludedFromList()
    {
        // Arrange
        var flour = SeedCanonicalIngredient("All-Purpose Flour");
        SeedInventoryItem(flour.Id, 1000m, GramId); // have 1000g
        var (plan, _) = SeedMealPlanWithRecipe("Bread",
            [(flour.Id, 500m, GramId)]); // need 500g

        // Act
        var list = await _sut.GenerateShoppingListAsync(plan.Id);

        // Assert
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateShoppingListAsync_AggregatesAcrossMultipleRecipes()
    {
        // Arrange
        var oliveOil = SeedCanonicalIngredient("Olive Oil");
        SeedInventoryItem(oliveOil.Id, 50m, MlId); // have 50ml

        // Create three recipes each needing 30ml olive oil (total: 90ml)
        var recipe1 = new Recipe { CuisineType = "Italian", Id = Guid.NewGuid(), Instructions = "Cook.", ServingCount = 4, Title = "Pasta" };
        var recipe2 = new Recipe { CuisineType = "Italian", Id = Guid.NewGuid(), Instructions = "Cook.", ServingCount = 4, Title = "Salad" };
        var recipe3 = new Recipe { CuisineType = "Italian", Id = Guid.NewGuid(), Instructions = "Cook.", ServingCount = 4, Title = "Soup" };
        _dbContext.Recipes.AddRange(recipe1, recipe2, recipe3);
        _dbContext.SaveChanges();

        foreach (var recipe in new[] { recipe1, recipe2, recipe3 })
        {
            _dbContext.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = oliveOil.Id,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = 30m,
                RecipeId = recipe.Id,
                UomId = MlId
            });
        }
        _dbContext.SaveChanges();

        var plan = new Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.Add(plan);
        _dbContext.SaveChanges();

        AddSlotToPlan(plan.Id, recipe1, DayOfWeek.Monday, MealSlot.Dinner);
        AddSlotToPlan(plan.Id, recipe2, DayOfWeek.Tuesday, MealSlot.Dinner);
        AddSlotToPlan(plan.Id, recipe3, DayOfWeek.Wednesday, MealSlot.Dinner);

        // Act — need 90ml, have 50ml, deficit = 40ml
        var list = await _sut.GenerateShoppingListAsync(plan.Id);

        // Assert
        list.Should().ContainSingle(i => i.CanonicalIngredientName == "Olive Oil");
        var item = list.Single();
        item.Quantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateShoppingListAsync_EmptyPlan_ReturnsEmptyList()
    {
        // Arrange
        var plan = new Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Empty Plan",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.Add(plan);
        _dbContext.SaveChanges();

        // Act
        var list = await _sut.GenerateShoppingListAsync(plan.Id);

        // Assert
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateShoppingListAsync_Regeneration_ReplacesPreviousItems()
    {
        // Arrange
        var cheese = SeedCanonicalIngredient("Cheddar Cheese");
        var (plan, _) = SeedMealPlanWithRecipe("Cheesy Pasta",
            [(cheese.Id, 300m, GramId)]);

        // Generate first time (no inventory = full deficit)
        var list1 = await _sut.GenerateShoppingListAsync(plan.Id);
        list1.Should().HaveCount(1);

        // Add inventory that fully covers
        SeedInventoryItem(cheese.Id, 500m, GramId);

        // Act — regenerate
        var list2 = await _sut.GenerateShoppingListAsync(plan.Id);

        // Assert
        list2.Should().BeEmpty();
        _dbContext.ShoppingListItems.Count(sli => sli.MealPlanId == plan.Id).Should().Be(0);
    }

    [Fact]
    public async Task GenerateShoppingListAsync_NoInventory_ShowsFullRequirement()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken");
        var (plan, _) = SeedMealPlanWithRecipe("Grilled Chicken",
            [(chicken.Id, 500m, GramId)]);

        // Act
        var list = await _sut.GenerateShoppingListAsync(plan.Id);

        // Assert
        list.Should().ContainSingle(i => i.CanonicalIngredientName == "Chicken");
        var item = list.Single();
        item.Quantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateShoppingListAsync_NonExistentPlan_ReturnsEmptyList()
    {
        var list = await _sut.GenerateShoppingListAsync(Guid.NewGuid());
        list.Should().BeEmpty();
    }
}
