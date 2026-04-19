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

        var conversionService = new UnitOfMeasureConversionService(_dbContext);
        var displayConverter = new UnitOfMeasureDisplayConverter(_dbContext);

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
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = EachId,
                Name = "Each",
                UnitOfMeasureType = UnitOfMeasureType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "g",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = GramId,
                Name = "Gram",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "ml",
                BaseUnitOfMeasureId = null,
                ConversionFactor = 1.0m,
                Id = MlId,
                Name = "Milliliter",
                UnitOfMeasureType = UnitOfMeasureType.Volume
            });
        _dbContext.SaveChanges();
    }

    private CanonicalIngredient SeedCanonicalIngredient(string name, IngredientCategory category = IngredientCategory.Other)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = category,
            DefaultUnitOfMeasureId = EachId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        _dbContext.SaveChanges();
        return ingredient;
    }

    private InventoryItem SeedInventoryItem(Guid canonicalIngredientId, decimal quantity, Guid unitOfMeasureId)
    {
        var item = new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Pantry,
            Quantity = quantity,
            UnitOfMeasureId = unitOfMeasureId
        };
        _dbContext.InventoryItems.Add(item);
        _dbContext.SaveChanges();
        return item;
    }

    private (Api.Models.Entities.MealPlan Plan, Recipe Recipe) SeedMealPlanWithRecipe(
        string recipeTitle,
        IEnumerable<(Guid IngredientId, decimal Quantity, Guid UnitOfMeasureId)> ingredientLines)
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

        foreach (var (ingredientId, qty, unitOfMeasureId) in ingredientLines)
        {
            _dbContext.RecipeIngredients.Add(new RecipeIngredient
            {
                CanonicalIngredientId = ingredientId,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = qty,
                RecipeId = recipe.Id,
                UnitOfMeasureId = unitOfMeasureId
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

    // Scenario: AddFromRecipeAsync returns empty when recipe not found
    //   Given no recipe exists for a given ID
    //   When AddFromRecipeAsync is called
    //   Then an empty list is returned

    [Fact]
    public async Task AddFromRecipeAsync_RecipeNotFound_ReturnsEmptyList()
    {
        // Arrange — non-existent recipe ID
        var missingId = Guid.NewGuid();

        // Act
        var result = await _sut.AddFromRecipeAsync(missingId);

        // Assert
        result.Should().BeEmpty();
    }

    // Scenario: AddFromRecipeAsync creates standalone shopping list items for missing ingredients
    //   Given a recipe with 500g of "Chicken" and no inventory on hand
    //   When AddFromRecipeAsync is called
    //   Then a standalone shopping list item for "Chicken" is created with a positive quantity

    [Fact]
    public async Task AddFromRecipeAsync_RecipeWithNoInventory_CreatesStandaloneItems()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken", IngredientCategory.Protein);
        var recipe = new Recipe
        {
            CuisineType = "American",
            Id = Guid.NewGuid(),
            Instructions = "Grill it.",
            ServingCount = 4,
            Title = "Grilled Chicken"
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.RecipeIngredients.Add(new RecipeIngredient
        {
            CanonicalIngredientId = chicken.Id,
            Id = Guid.NewGuid(),
            IsContainerResolved = true,
            Quantity = 500m,
            RecipeId = recipe.Id,
            UnitOfMeasureId = GramId
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.AddFromRecipeAsync(recipe.Id);

        // Assert
        result.Should().ContainSingle(i => i.CanonicalIngredientName == "Chicken");
        result.Single().Quantity.Should().BeGreaterThan(0);
    }

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
                UnitOfMeasureId = MlId
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

    // Scenario: GetShoppingListAsync returns empty when no items exist for plan
    //   Given a meal plan with no shopping list items persisted
    //   When GetShoppingListAsync is called
    //   Then an empty list is returned

    [Fact]
    public async Task GetShoppingListAsync_NoItemsForPlan_ReturnsEmptyList()
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
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetShoppingListAsync(plan.Id);

        // Assert
        result.Should().BeEmpty();
    }

    // Scenario: GetShoppingListAsync returns items only for the requested plan
    //   Given two plans each with one shopping list item
    //   When GetShoppingListAsync is called for plan A
    //   Then only plan A's item is returned

    [Fact]
    public async Task GetShoppingListAsync_TwoPlansWithItems_ReturnsOnlyItemsForRequestedPlan()
    {
        // Arrange
        var ingredient = SeedCanonicalIngredient("Butter", IngredientCategory.Dairy);

        var planA = new Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Plan A",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        var planB = new Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Plan B",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.AddRange(planA, planB);

        _dbContext.ShoppingListItems.AddRange(
            new ShoppingListItem
            {
                CanonicalIngredientId = ingredient.Id,
                Id = Guid.NewGuid(),
                MealPlanId = planA.Id,
                Quantity = 100m,
                UnitOfMeasureId = GramId
            },
            new ShoppingListItem
            {
                CanonicalIngredientId = ingredient.Id,
                Id = Guid.NewGuid(),
                MealPlanId = planB.Id,
                Quantity = 200m,
                UnitOfMeasureId = GramId
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetShoppingListAsync(planA.Id);

        // Assert
        result.Should().ContainSingle();
    }

    // Scenario: GetStandaloneShoppingListAsync returns empty when no standalone items exist
    //   Given no standalone shopping list items
    //   When GetStandaloneShoppingListAsync is called
    //   Then an empty list is returned

    [Fact]
    public async Task GetStandaloneShoppingListAsync_NoItems_ReturnsEmptyList()
    {
        // Arrange — nothing seeded beyond reference UOMs

        // Act
        var result = await _sut.GetStandaloneShoppingListAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // Scenario: GetStandaloneShoppingListAsync does not return plan-scoped items
    //   Given one plan-scoped item and one standalone item
    //   When GetStandaloneShoppingListAsync is called
    //   Then only the standalone item is returned

    [Fact]
    public async Task GetStandaloneShoppingListAsync_MixOfStandaloneAndPlanItems_ReturnsOnlyStandaloneItems()
    {
        // Arrange
        var ingredient = SeedCanonicalIngredient("Milk", IngredientCategory.Dairy);

        var plan = new Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Some Plan",
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _dbContext.MealPlans.Add(plan);

        _dbContext.ShoppingListItems.AddRange(
            new ShoppingListItem
            {
                CanonicalIngredientId = ingredient.Id,
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,   // plan-scoped — should NOT appear
                Quantity = 500m,
                UnitOfMeasureId = MlId
            },
            new ShoppingListItem
            {
                CanonicalIngredientId = ingredient.Id,
                Id = Guid.NewGuid(),
                MealPlanId = null,      // standalone — SHOULD appear
                Quantity = 250m,
                UnitOfMeasureId = MlId
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetStandaloneShoppingListAsync();

        // Assert
        result.Should().ContainSingle();
    }
}
