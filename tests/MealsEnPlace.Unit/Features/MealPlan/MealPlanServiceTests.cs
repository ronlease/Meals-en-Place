// Feature: Meal Plan Generation
//
// Scenario: Generate a weekly meal plan
//   Given I have recipes in my library and items in my inventory
//   When I request a meal plan for the current week
//   Then the system generates a plan assigning recipes to day/slot combinations
//   And the plan covers the requested date range
//
// Scenario: No recipe repeats within the generated plan
//   Given a meal plan is being generated for 7 days
//   When the system assigns recipes to slots
//   Then no recipe appears more than once in the plan
//
// Scenario: Prioritize expiry-imminent ingredients
//   Given I have "Salmon" in my fridge expiring in 3 days
//   And a recipe "Grilled Salmon" uses "Salmon"
//   When the system generates a meal plan
//   Then "Grilled Salmon" is prioritized for an early slot in the plan
//
// Scenario: Respect dietary filter preferences
//   Given I request a meal plan with dietary filter "Vegetarian"
//   When the system generates the plan
//   Then all assigned recipes are tagged "Vegetarian"
//
// Scenario: Swap a meal plan slot
//   Given a meal plan has been generated
//   When I swap a slot to a different recipe
//   Then the slot now shows the new recipe
//
// Scenario: Generate with empty recipe library returns plan with no slots
//   Given no recipes exist
//   When I generate a meal plan
//   Then the plan has zero slots
//
// Scenario: Get active returns most recent plan
//   Given two meal plans exist for different weeks
//   When I request the active plan
//   Then the most recent plan is returned

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.MealPlan;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MealsEnPlace.Unit.Features.MealPlan;

public class MealPlanServiceTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IClaudeService> _claudeServiceMock = new(MockBehavior.Loose);
    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly MealPlanService _sut;

    private static readonly Guid EachId = UnitOfMeasureConfiguration.EachId;
    private static readonly Guid GramId = UnitOfMeasureConfiguration.GramId;
    private static readonly Guid MlId = UnitOfMeasureConfiguration.MlId;

    public MealPlanServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MealsEnPlaceDbContext(options);
        SeedUnitOfMeasures();

        _claudeServiceMock
            .Setup(c => c.OptimizeMealPlanAsync(
                It.IsAny<IReadOnlyList<MealPlanSlotCandidate>>(),
                It.IsAny<IReadOnlyList<InventoryItem>>(),
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<MealPlanSlotCandidate>, IReadOnlyList<InventoryItem>, CancellationToken>(
                (candidates, _, _) => Task.FromResult(candidates));

        var conversionService = new UnitOfMeasureConversionService(_dbContext);

        _sut = new MealPlanService(
            _claudeServiceMock.Object,
            _dbContext,
            conversionService);
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

    private CanonicalIngredient SeedCanonicalIngredient(string name)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = EachId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        _dbContext.SaveChanges();
        return ingredient;
    }

    private InventoryItem SeedInventoryItem(
        Guid canonicalIngredientId,
        decimal quantity,
        Guid unitOfMeasureId,
        DateOnly? expiryDate = null)
    {
        var item = new InventoryItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            ExpiryDate = expiryDate,
            Id = Guid.NewGuid(),
            Location = StorageLocation.Fridge,
            Quantity = quantity,
            UnitOfMeasureId = unitOfMeasureId
        };
        _dbContext.InventoryItems.Add(item);
        _dbContext.SaveChanges();
        return item;
    }

    private Recipe SeedFullyResolvedRecipe(
        string title,
        string cuisineType,
        IEnumerable<(Guid IngredientId, decimal Quantity, Guid UnitOfMeasureId)> ingredientLines,
        List<DietaryTag>? dietaryTags = null)
    {
        var recipe = new Recipe
        {
            CuisineType = cuisineType,
            Id = Guid.NewGuid(),
            Instructions = "Cook it.",
            ServingCount = 4,
            Title = title
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

        if (dietaryTags != null)
        {
            foreach (var tag in dietaryTags)
            {
                _dbContext.RecipeDietaryTags.Add(new RecipeDietaryTag
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Tag = tag
                });
            }
        }

        _dbContext.SaveChanges();
        return recipe;
    }

    private static GenerateMealPlanRequest DefaultRequest() => new()
    {
        WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        SlotPreferences = new Dictionary<DayOfWeek, List<MealSlot>>
        {
            [DayOfWeek.Monday] = [MealSlot.Dinner],
            [DayOfWeek.Tuesday] = [MealSlot.Dinner]
        }
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateMealPlanAsync_WithRecipesAndInventory_AssignsRecipesToSlots()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken");
        var pasta = SeedCanonicalIngredient("Pasta");
        SeedInventoryItem(chicken.Id, 500m, GramId);
        SeedInventoryItem(pasta.Id, 500m, GramId);

        SeedFullyResolvedRecipe("Grilled Chicken", "American", [(chicken.Id, 200m, GramId)]);
        SeedFullyResolvedRecipe("Pasta Aglio", "Italian", [(pasta.Id, 200m, GramId)]);

        // Act
        var result = await _sut.GenerateMealPlanAsync(DefaultRequest());

        // Assert
        result.Slots.Should().HaveCount(2);
        result.WeekStartDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task GenerateMealPlanAsync_NoRecipeRepeatsInPlan()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken");
        var pasta = SeedCanonicalIngredient("Pasta");
        var rice = SeedCanonicalIngredient("Rice");
        SeedInventoryItem(chicken.Id, 500m, GramId);
        SeedInventoryItem(pasta.Id, 500m, GramId);
        SeedInventoryItem(rice.Id, 500m, GramId);

        SeedFullyResolvedRecipe("Grilled Chicken", "American", [(chicken.Id, 200m, GramId)]);
        SeedFullyResolvedRecipe("Pasta Aglio", "Italian", [(pasta.Id, 200m, GramId)]);
        SeedFullyResolvedRecipe("Fried Rice", "Chinese", [(rice.Id, 200m, GramId)]);

        var request = new GenerateMealPlanRequest
        {
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SlotPreferences = new Dictionary<DayOfWeek, List<MealSlot>>
            {
                [DayOfWeek.Monday] = [MealSlot.Dinner],
                [DayOfWeek.Tuesday] = [MealSlot.Dinner],
                [DayOfWeek.Wednesday] = [MealSlot.Dinner]
            }
        };

        // Act
        var result = await _sut.GenerateMealPlanAsync(request);

        // Assert
        result.Slots.Select(s => s.RecipeId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GenerateMealPlanAsync_ExpiryImminentIngredients_PrioritizedForEarlySlots()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var salmon = SeedCanonicalIngredient("Salmon");
        var chicken = SeedCanonicalIngredient("Chicken");
        SeedInventoryItem(salmon.Id, 500m, GramId, today.AddDays(2)); // expiring soon
        SeedInventoryItem(chicken.Id, 500m, GramId, today.AddDays(30)); // not expiring

        var salmonRecipe = SeedFullyResolvedRecipe("Grilled Salmon", "American", [(salmon.Id, 200m, GramId)]);
        SeedFullyResolvedRecipe("Grilled Chicken", "American", [(chicken.Id, 200m, GramId)]);

        var request = new GenerateMealPlanRequest
        {
            WeekStartDate = today,
            SlotPreferences = new Dictionary<DayOfWeek, List<MealSlot>>
            {
                [today.DayOfWeek] = [MealSlot.Dinner],
                [today.AddDays(1).DayOfWeek] = [MealSlot.Dinner]
            }
        };

        // Act
        var result = await _sut.GenerateMealPlanAsync(request);

        // Assert — salmon recipe should be first (highest scored due to waste bonus)
        result.Slots.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Slots.First().RecipeId.Should().Be(salmonRecipe.Id);
    }

    [Fact]
    public async Task GenerateMealPlanAsync_DietaryFilter_OnlyMatchingRecipes()
    {
        // Arrange
        var tofu = SeedCanonicalIngredient("Tofu");
        var chicken = SeedCanonicalIngredient("Chicken");
        SeedInventoryItem(tofu.Id, 500m, GramId);
        SeedInventoryItem(chicken.Id, 500m, GramId);

        var vegRecipe = SeedFullyResolvedRecipe("Tofu Stir Fry", "Chinese",
            [(tofu.Id, 200m, GramId)], [DietaryTag.Vegetarian]);
        SeedFullyResolvedRecipe("Chicken Wings", "American",
            [(chicken.Id, 200m, GramId)]);

        var request = new GenerateMealPlanRequest
        {
            DietaryTags = [DietaryTag.Vegetarian],
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SlotPreferences = new Dictionary<DayOfWeek, List<MealSlot>>
            {
                [DayOfWeek.Monday] = [MealSlot.Dinner]
            }
        };

        // Act
        var result = await _sut.GenerateMealPlanAsync(request);

        // Assert
        result.Slots.Should().ContainSingle();
        result.Slots[0].RecipeId.Should().Be(vegRecipe.Id);
    }

    [Fact]
    public async Task SwapSlotAsync_ValidSlotAndRecipe_ReturnsUpdatedSlot()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken");
        var pasta = SeedCanonicalIngredient("Pasta");
        SeedInventoryItem(chicken.Id, 500m, GramId);
        SeedInventoryItem(pasta.Id, 500m, GramId);

        SeedFullyResolvedRecipe("Grilled Chicken", "American", [(chicken.Id, 200m, GramId)]);
        var pastaRecipe = SeedFullyResolvedRecipe("Pasta Aglio", "Italian", [(pasta.Id, 200m, GramId)]);

        var plan = await _sut.GenerateMealPlanAsync(DefaultRequest());
        var slotToSwap = plan.Slots.First();

        // Act
        var result = await _sut.SwapSlotAsync(slotToSwap.Id, new SwapSlotRequest { RecipeId = pastaRecipe.Id });

        // Assert
        result.Should().NotBeNull();
        result!.RecipeId.Should().Be(pastaRecipe.Id);
        result.RecipeTitle.Should().Be("Pasta Aglio");
    }

    [Fact]
    public async Task SwapSlotAsync_NonExistentSlot_ReturnsNull()
    {
        var result = await _sut.SwapSlotAsync(Guid.NewGuid(), new SwapSlotRequest { RecipeId = Guid.NewGuid() });
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateMealPlanAsync_NoRecipes_ReturnsEmptyPlan()
    {
        // Act
        var result = await _sut.GenerateMealPlanAsync(DefaultRequest());

        // Assert
        result.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveMealPlanAsync_MultiplePlans_ReturnsMostRecent()
    {
        // Arrange
        var chicken = SeedCanonicalIngredient("Chicken");
        SeedInventoryItem(chicken.Id, 500m, GramId);
        SeedFullyResolvedRecipe("Grilled Chicken", "American", [(chicken.Id, 200m, GramId)]);

        var olderRequest = new GenerateMealPlanRequest
        {
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7),
            SlotPreferences = new Dictionary<DayOfWeek, List<MealSlot>>
            {
                [DayOfWeek.Monday] = [MealSlot.Dinner]
            }
        };
        await _sut.GenerateMealPlanAsync(olderRequest);

        // Need a second recipe for the newer plan (can't repeat)
        var pasta = SeedCanonicalIngredient("Pasta");
        SeedInventoryItem(pasta.Id, 500m, GramId);
        SeedFullyResolvedRecipe("Pasta Aglio", "Italian", [(pasta.Id, 200m, GramId)]);

        var newerRequest = new GenerateMealPlanRequest
        {
            WeekStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SlotPreferences = new Dictionary<DayOfWeek, List<MealSlot>>
            {
                [DayOfWeek.Monday] = [MealSlot.Dinner]
            }
        };
        var newerPlan = await _sut.GenerateMealPlanAsync(newerRequest);

        // Act
        var active = await _sut.GetActiveMealPlanAsync();

        // Assert
        active.Should().NotBeNull();
        active!.Id.Should().Be(newerPlan.Id);
    }

    [Fact]
    public async Task GetActiveMealPlanAsync_NoPlans_ReturnsNull()
    {
        var result = await _sut.GetActiveMealPlanAsync();
        result.Should().BeNull();
    }
}
