// Feature: Recipe Library Import
//
// Scenario: Import creates recipe with correct title and cuisine
//   Given a TheMealDbMeal with MealName "Chicken Tikka Masala" and Area "Indian"
//   When ImportByIdAsync is called
//   Then the saved Recipe has Title "Chicken Tikka Masala"
//   And the saved Recipe has CuisineType "Indian"
//
// Scenario: Import detects container reference and sets IsContainerResolved = false
//   Given a TheMealDbMeal whose first measure string contains "can"
//   When ImportByIdAsync is called
//   Then the corresponding RecipeIngredient has IsContainerResolved = false
//   And the RecipeIngredient has UnitOfMeasureId = null
//   And the RecipeIngredient Notes preserves the original measure string
//
// Scenario: Import normalizes a standard unit of measure deterministically
//   Given a TheMealDbMeal with measure string "2 cups" for one ingredient
//   And IUnitOfMeasureNormalizationService returns a high-confidence result for "2 cups"
//   When ImportByIdAsync is called
//   Then the corresponding RecipeIngredient has IsContainerResolved = true
//   And the RecipeIngredient Quantity equals the value from the normalization result
//
// Scenario: Import creates CanonicalIngredient when not found
//   Given a TheMealDbMeal with ingredient "Porcini Mushrooms" not already in the database
//   When ImportByIdAsync is called
//   Then a new CanonicalIngredient with Name "Porcini Mushrooms" is created
//
// Scenario: Import reuses existing CanonicalIngredient with case-insensitive match
//   Given a CanonicalIngredient "Olive Oil" already exists in the database
//   And a TheMealDbMeal has ingredient "olive oil" (lowercase)
//   When ImportByIdAsync is called
//   Then no second CanonicalIngredient is created
//   And the RecipeIngredient references the pre-existing CanonicalIngredient
//
// Scenario: Duplicate import throws InvalidOperationException
//   Given a recipe with TheMealDbId "12345" has already been imported
//   When ImportByIdAsync is called again for "12345"
//   Then an InvalidOperationException is thrown
//   And no duplicate Recipe record is created
//
// Scenario: TheMealDB returns null meal — throws InvalidOperationException
//   Given ITheMealDbClient.GetByIdAsync returns null for a given ID
//   When ImportByIdAsync is called
//   Then an InvalidOperationException is thrown
//
// Scenario: Empty ingredient slots are skipped
//   Given a TheMealDbMeal where Ingredient1 is "Pasta" and Ingredient2 through Ingredient20 are null
//   When ImportByIdAsync is called
//   Then exactly one RecipeIngredient is created

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MealsEnPlace.Unit.Features.Recipes;

public class RecipeImportServiceTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IClaudeAvailability> _claudeAvailabilityMock = new(MockBehavior.Loose);
    private readonly Mock<IClaudeService> _claudeServiceMock = new(MockBehavior.Loose);
    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly Mock<ITheMealDbClient> _theMealDbClientMock = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfMeasureNormalizationService> _unitOfMeasureNormalizationServiceMock = new(MockBehavior.Loose);
    private readonly RecipeImportService _sut;

    // Stable IDs for seeded unit of measure reference data
    private static readonly Guid EachUnitOfMeasureId = new("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid GramUnitOfMeasureId = new("a1000000-0000-0000-0000-000000000002");

    public RecipeImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MealsEnPlaceDbContext(options);

        // Claude is called for dietary classification; return empty list to avoid complexity
        _claudeServiceMock
            .Setup(c => c.ClassifyDietaryTagsAsync(It.IsAny<Recipe>()))
            .ReturnsAsync(Array.Empty<DietaryTag>());

        // Default: normalization returns a high-confidence gram result for any measure string
        _unitOfMeasureNormalizationServiceMock
            .Setup(n => n.NormalizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NormalizationResult
            {
                Confidence = ClaudeConfidence.High,
                Quantity = 250m,
                UnitOfMeasureAbbreviation = "g",
                UnitOfMeasureId = GramUnitOfMeasureId,
                WasClaudeResolved = false
            });

        SeedReferenceData();

        _claudeAvailabilityMock
            .Setup(a => a.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new RecipeImportService(
            _claudeAvailabilityMock.Object,
            _claudeServiceMock.Object,
            _dbContext,
            NullLogger<RecipeImportService>.Instance,
            _theMealDbClientMock.Object,
            _unitOfMeasureNormalizationServiceMock.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedReferenceData()
    {
        _dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "ea",
                ConversionFactor = 1.0m,
                Id = EachUnitOfMeasureId,
                Name = "Each",
                UnitOfMeasureType = UnitOfMeasureType.Count
            },
            new UnitOfMeasure
            {
                Abbreviation = "g",
                ConversionFactor = 1.0m,
                Id = GramUnitOfMeasureId,
                Name = "Gram",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            });

        _dbContext.SaveChanges();
    }

    private static TheMealDbMeal BuildMeal(
        string mealId = "52772",
        string mealName = "Chicken Tikka Masala",
        string area = "Indian",
        string? ingredient1 = "Chicken",
        string? measure1 = "500g",
        string? ingredient2 = null,
        string? measure2 = null) =>
        new()
        {
            Area = area,
            Category = "Chicken",
            Instructions = "Cook the chicken.",
            MealId = mealId,
            MealName = mealName,
            Ingredient1 = ingredient1,
            Measure1 = measure1,
            Ingredient2 = ingredient2,
            Measure2 = measure2
        };

    // ── ImportByIdAsync — title and cuisine ───────────────────────────────────

    [Fact]
    public async Task ImportByIdAsync_ValidMeal_SavesRecipeWithCorrectTitle()
    {
        // Arrange
        var meal = BuildMeal(mealName: "Chicken Tikka Masala");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var saved = await _dbContext.Recipes.AsNoTracking().FirstAsync(r => r.Id == result.RecipeId);
        saved.Title.Should().Be("Chicken Tikka Masala");
    }

    [Fact]
    public async Task ImportByIdAsync_ValidMeal_SavesRecipeWithCorrectCuisineType()
    {
        // Arrange
        var meal = BuildMeal(area: "Indian");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var saved = await _dbContext.Recipes.AsNoTracking().FirstAsync(r => r.Id == result.RecipeId);
        saved.CuisineType.Should().Be("Indian");
    }

    // ── ImportByIdAsync — container reference detection ───────────────────────

    [Fact]
    public async Task ImportByIdAsync_MeasureContainsCanKeyword_RecipeIngredientHasIsContainerResolvedFalse()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "Diced Tomatoes", measure1: "1 can");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.RecipeId);
        ingredient.IsContainerResolved.Should().BeFalse();
    }

    [Fact]
    public async Task ImportByIdAsync_MeasureContainsCanKeyword_RecipeIngredientHasNullUnitOfMeasureId()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "Diced Tomatoes", measure1: "1 can");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.RecipeId);
        ingredient.UnitOfMeasureId.Should().BeNull();
    }

    [Fact]
    public async Task ImportByIdAsync_MeasureContainsCanKeyword_NotesPreservesOriginalMeasureString()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "Diced Tomatoes", measure1: "1 can");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.RecipeId);
        ingredient.Notes.Should().Be("1 can");
    }

    [Fact]
    public async Task ImportByIdAsync_MeasureContainsJarKeyword_RecipeIngredientHasIsContainerResolvedFalse()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "Marinara Sauce", measure1: "2 jars");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.RecipeId);
        ingredient.IsContainerResolved.Should().BeFalse();
    }

    [Fact]
    public async Task ImportByIdAsync_ResultUnresolvedCount_ReflectsNumberOfContainerReferenceIngredients()
    {
        // Arrange — two ingredients, both container references
        var meal = new TheMealDbMeal
        {
            Area = "Italian",
            Instructions = "Cook.",
            MealId = "52772",
            MealName = "Pasta Bake",
            Ingredient1 = "Diced Tomatoes",
            Measure1 = "1 can",
            Ingredient2 = "Kidney Beans",
            Measure2 = "1 can"
        };
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        result.UnresolvedCount.Should().Be(2);
    }

    // ── ImportByIdAsync — standard unit of measure normalization ──────────────────────────

    [Fact]
    public async Task ImportByIdAsync_StandardMeasureString_RecipeIngredientHasIsContainerResolvedTrue()
    {
        // Arrange — "2 cups" is a standard unit of measure, not a container reference
        var meal = BuildMeal(ingredient1: "All-Purpose Flour", measure1: "2 cups");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.RecipeId);
        ingredient.IsContainerResolved.Should().BeTrue();
    }

    [Fact]
    public async Task ImportByIdAsync_StandardMeasureString_RecipeIngredientQuantityMatchesNormalizationResult()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "All-Purpose Flour", measure1: "2 cups");
        _unitOfMeasureNormalizationServiceMock
            .Setup(n => n.NormalizeAsync("2 cups", "All-Purpose Flour", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NormalizationResult
            {
                Confidence = ClaudeConfidence.High,
                Quantity = 473.18m,
                UnitOfMeasureAbbreviation = "ml",
                UnitOfMeasureId = GramUnitOfMeasureId,
                WasClaudeResolved = false
            });

        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.RecipeId);
        ingredient.Quantity.Should().Be(473.18m);
    }

    [Fact]
    public async Task ImportByIdAsync_StandardMeasureString_UnitOfMeasureNormalizationServiceCalledWithCorrectArguments()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "Sugar", measure1: "1 cup");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        await _sut.ImportByIdAsync("52772");

        // Assert
        _unitOfMeasureNormalizationServiceMock.Verify(
            n => n.NormalizeAsync("1 cup", "Sugar", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── ImportByIdAsync — CanonicalIngredient creation ────────────────────────

    [Fact]
    public async Task ImportByIdAsync_NewIngredientName_CreatesCanonicalIngredient()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "Porcini Mushrooms", measure1: "100g");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        await _sut.ImportByIdAsync("52772");

        // Assert
        var canonical = await _dbContext.CanonicalIngredients.AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.Name == "Porcini Mushrooms");
        canonical.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportByIdAsync_NewIngredientName_OnlyOneCanonicalIngredientCreated()
    {
        // Arrange
        var meal = BuildMeal(ingredient1: "Porcini Mushrooms", measure1: "100g");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        await _sut.ImportByIdAsync("52772");

        // Assert
        var count = await _dbContext.CanonicalIngredients.AsNoTracking()
            .CountAsync(ci => ci.Name == "Porcini Mushrooms");
        count.Should().Be(1);
    }

    // ── ImportByIdAsync — CanonicalIngredient reuse (case-insensitive) ─────────

    [Fact]
    public async Task ImportByIdAsync_IngredientNameDiffersOnlyInCase_ReusesExistingCanonicalIngredient()
    {
        // Arrange — seed "Olive Oil" with Title Case; recipe arrives as "olive oil"
        var existingIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = EachUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Olive Oil"
        };
        _dbContext.CanonicalIngredients.Add(existingIngredient);
        await _dbContext.SaveChangesAsync();

        var meal = BuildMeal(ingredient1: "olive oil", measure1: "30ml");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert — still only one CanonicalIngredient for "Olive Oil"
        var count = await _dbContext.CanonicalIngredients.AsNoTracking()
            .CountAsync(ci => ci.Name.ToLower() == "olive oil");
        count.Should().Be(1);

        // And the RecipeIngredient references the pre-existing canonical ingredient
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.RecipeId);
        ingredient.CanonicalIngredientId.Should().Be(existingIngredient.Id);
    }

    // ── ImportByIdAsync — duplicate import ────────────────────────────────────

    [Fact]
    public async Task ImportByIdAsync_RecipeAlreadyImported_ThrowsInvalidOperationException()
    {
        // Arrange — seed a recipe with the same TheMealDbId
        _dbContext.Recipes.Add(new Recipe
        {
            CuisineType = "Indian",
            Id = Guid.NewGuid(),
            Instructions = "Cook.",
            ServingCount = 4,
            TheMealDbId = "52772",
            Title = "Chicken Tikka Masala"
        });
        await _dbContext.SaveChangesAsync();

        // The client should not be reached, but set it up defensively
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(BuildMeal());

        // Act
        var act = async () => await _sut.ImportByIdAsync("52772");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*52772*");
    }

    [Fact]
    public async Task ImportByIdAsync_RecipeAlreadyImported_NoDuplicateRecordCreated()
    {
        // Arrange
        _dbContext.Recipes.Add(new Recipe
        {
            CuisineType = "Indian",
            Id = Guid.NewGuid(),
            Instructions = "Cook.",
            ServingCount = 4,
            TheMealDbId = "52772",
            Title = "Chicken Tikka Masala"
        });
        await _dbContext.SaveChangesAsync();

        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(BuildMeal());

        // Act
        try { await _sut.ImportByIdAsync("52772"); } catch (InvalidOperationException) { }

        // Assert
        var count = await _dbContext.Recipes.AsNoTracking()
            .CountAsync(r => r.TheMealDbId == "52772");
        count.Should().Be(1);
    }

    // ── ImportByIdAsync — null meal from API ──────────────────────────────────

    [Fact]
    public async Task ImportByIdAsync_TheMealDbReturnsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("99999", It.IsAny<CancellationToken>()))
                            .ReturnsAsync((TheMealDbMeal?)null);

        // Act
        var act = async () => await _sut.ImportByIdAsync("99999");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*99999*");
    }

    // ── ImportByIdAsync — empty ingredient slots ──────────────────────────────

    [Fact]
    public async Task ImportByIdAsync_OnlyFirstIngredientSlotPopulated_CreatesExactlyOneRecipeIngredient()
    {
        // Arrange — Ingredient1 set, Ingredient2-20 are null (TheMealDbMeal default)
        var meal = new TheMealDbMeal
        {
            Area = "Italian",
            Instructions = "Boil pasta.",
            MealId = "52772",
            MealName = "Simple Pasta",
            Ingredient1 = "Pasta",
            Measure1 = "200g"
        };
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var count = await _dbContext.RecipeIngredients.AsNoTracking()
            .CountAsync(ri => ri.RecipeId == result.RecipeId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ImportByIdAsync_OnlyFirstIngredientSlotPopulated_TotalIngredientsInResultIsOne()
    {
        // Arrange
        var meal = new TheMealDbMeal
        {
            Area = "Italian",
            Instructions = "Boil pasta.",
            MealId = "52772",
            MealName = "Simple Pasta",
            Ingredient1 = "Pasta",
            Measure1 = "200g"
        };
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        result.TotalIngredients.Should().Be(1);
    }

    // ── ImportByIdAsync — all-container-reference recipe (critical edge case #11) ──

    [Fact]
    public async Task ImportByIdAsync_AllIngredientMeasuresAreContainerReferences_AllRecipeIngredientsAreUnresolved()
    {
        // Arrange — critical edge case #11: every measure is a container reference
        var meal = new TheMealDbMeal
        {
            Area = "Mexican",
            Instructions = "Mix and heat.",
            MealId = "52772",
            MealName = "Chili Con Carne",
            Ingredient1 = "Diced Tomatoes",
            Measure1 = "1 can",
            Ingredient2 = "Kidney Beans",
            Measure2 = "1 can",
            Ingredient3 = "Tomato Paste",
            Measure3 = "1 tube"
        };
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        result.UnresolvedCount.Should().Be(3);

        var ingredients = await _dbContext.RecipeIngredients.AsNoTracking()
            .Where(ri => ri.RecipeId == result.RecipeId)
            .ToListAsync();

        ingredients.Should().AllSatisfy(ri =>
        {
            ri.IsContainerResolved.Should().BeFalse();
            ri.UnitOfMeasureId.Should().BeNull();
        });
    }

    [Fact]
    public async Task ImportByIdAsync_AllIngredientMeasuresAreContainerReferences_ImportSucceeds()
    {
        // Arrange — critical edge case #11: import must not fail; all are flagged unresolved
        var meal = new TheMealDbMeal
        {
            Area = "Mexican",
            Instructions = "Mix and heat.",
            MealId = "52772",
            MealName = "Chili Con Carne",
            Ingredient1 = "Diced Tomatoes",
            Measure1 = "1 can",
            Ingredient2 = "Kidney Beans",
            Measure2 = "1 can"
        };
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var act = async () => await _sut.ImportByIdAsync("52772");

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── SearchAsync — mapped results ──────────────────────────────────────────

    // Scenario: SearchAsync returns mapped results from TheMealDB
    //   Given SearchByNameAsync returns two meals for "chicken"
    //   When SearchAsync is called with "chicken"
    //   Then the result list contains both meals with correct titles

    [Fact]
    public async Task SearchAsync_TheMealDbReturnsTwoMeals_ReturnsTwoMappedResults()
    {
        // Arrange
        var meals = new List<TheMealDbMeal>
        {
            new() { Category = "Chicken", MealId = "11111", MealName = "Chicken Curry" },
            new() { Category = "Chicken", MealId = "22222", MealName = "Chicken Soup" }
        };
        _theMealDbClientMock
            .Setup(c => c.SearchByNameAsync("chicken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(meals);

        // Act
        var result = await _sut.SearchAsync("chicken");

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.Title).Should().Contain(["Chicken Curry", "Chicken Soup"]);
    }

    // ── SearchAsync — already imported ────────────────────────────────────────

    // Scenario: SearchAsync marks meals as AlreadyImported when they exist in local DB
    //   Given a recipe with TheMealDbId "11111" has already been imported
    //   And SearchByNameAsync returns that meal plus a new one
    //   When SearchAsync is called
    //   Then the result for "11111" has AlreadyImported = true
    //   And the result for the new meal has AlreadyImported = false

    [Fact]
    public async Task SearchAsync_OneMealAlreadyImported_MarksAlreadyImportedCorrectly()
    {
        // Arrange — seed an already-imported recipe
        _dbContext.Recipes.Add(new Recipe
        {
            CuisineType = "Indian",
            Id = Guid.NewGuid(),
            Instructions = "Cook.",
            ServingCount = 4,
            TheMealDbId = "11111",
            Title = "Chicken Curry"
        });
        await _dbContext.SaveChangesAsync();

        var meals = new List<TheMealDbMeal>
        {
            new() { Category = "Chicken", MealId = "11111", MealName = "Chicken Curry" },
            new() { Category = "Chicken", MealId = "99999", MealName = "New Dish" }
        };
        _theMealDbClientMock
            .Setup(c => c.SearchByNameAsync("chicken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(meals);

        // Act
        var result = await _sut.SearchAsync("chicken");

        // Assert
        result.First(r => r.Id == "11111").AlreadyImported.Should().BeTrue();
        result.First(r => r.Id == "99999").AlreadyImported.Should().BeFalse();
    }

    // ── SearchAsync — empty results ────────────────────────────────────────────

    // Scenario: SearchAsync returns empty list when no meals found
    //   Given SearchByNameAsync returns an empty list
    //   When SearchAsync is called
    //   Then an empty list is returned

    [Fact]
    public async Task SearchAsync_TheMealDbReturnsEmpty_ReturnsEmptyList()
    {
        // Arrange
        _theMealDbClientMock
            .Setup(c => c.SearchByNameAsync("xyznothing", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.SearchAsync("xyznothing");

        // Assert
        result.Should().BeEmpty();
    }

    // ── SearchByCategoryAsync — mapped results ────────────────────────────────

    // Scenario: SearchByCategoryAsync returns mapped results from TheMealDB
    //   Given FilterByCategoryAsync returns one meal for category "Seafood"
    //   When SearchByCategoryAsync is called with "Seafood"
    //   Then the result contains that meal

    [Fact]
    public async Task SearchByCategoryAsync_TheMealDbReturnsOneMeal_ReturnsMappedResult()
    {
        // Arrange
        var meals = new List<TheMealDbMeal>
        {
            new() { Category = "Seafood", MealId = "33333", MealName = "Grilled Salmon" }
        };
        _theMealDbClientMock
            .Setup(c => c.FilterByCategoryAsync("Seafood", It.IsAny<CancellationToken>()))
            .ReturnsAsync(meals);

        // Act
        var result = await _sut.SearchByCategoryAsync("Seafood");

        // Assert
        result.Should().ContainSingle();
        result[0].Title.Should().Be("Grilled Salmon");
        result[0].Id.Should().Be("33333");
    }

    // ── SearchByCategoryAsync — empty results ─────────────────────────────────

    // Scenario: SearchByCategoryAsync returns empty list when no meals found
    //   Given FilterByCategoryAsync returns an empty list
    //   When SearchByCategoryAsync is called
    //   Then an empty list is returned

    [Fact]
    public async Task SearchByCategoryAsync_TheMealDbReturnsEmpty_ReturnsEmptyList()
    {
        // Arrange
        _theMealDbClientMock
            .Setup(c => c.FilterByCategoryAsync("UnknownCategory", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.SearchByCategoryAsync("UnknownCategory");

        // Assert
        result.Should().BeEmpty();
    }

    // ── CreateRecipeAsync — correct title ─────────────────────────────────────

    // Scenario: CreateRecipeAsync creates recipe with correct title
    //   Given a CreateRecipeRequest with Title "Homemade Tacos"
    //   And one ingredient with a resolved UnitOfMeasureId
    //   When CreateRecipeAsync is called
    //   Then the saved Recipe has Title "Homemade Tacos"

    [Fact]
    public async Task CreateRecipeAsync_ValidRequest_SavesRecipeWithCorrectTitle()
    {
        // Arrange
        var canonicalIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Protein,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Beef"
        };
        _dbContext.CanonicalIngredients.Add(canonicalIngredient);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRecipeRequest
        {
            CuisineType = "Mexican",
            Ingredients =
            [
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = canonicalIngredient.Id,
                    Quantity = 500m,
                    UnitOfMeasureId = GramUnitOfMeasureId
                }
            ],
            Instructions = "Cook the beef.",
            ServingCount = 4,
            Title = "Homemade Tacos"
        };

        // Act
        var result = await _sut.CreateRecipeAsync(request);

        // Assert
        result.Title.Should().Be("Homemade Tacos");
    }

    // ── CreateRecipeAsync — creates ingredients ────────────────────────────────

    // Scenario: CreateRecipeAsync creates recipe ingredients
    //   Given a CreateRecipeRequest with two ingredients
    //   When CreateRecipeAsync is called
    //   Then the saved Recipe has exactly two RecipeIngredients

    [Fact]
    public async Task CreateRecipeAsync_ValidRequestWithTwoIngredients_CreatesTwoRecipeIngredients()
    {
        // Arrange
        var ingredient1 = new CanonicalIngredient
        {
            Category = IngredientCategory.Protein,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Chicken Breast"
        };
        var ingredient2 = new CanonicalIngredient
        {
            Category = IngredientCategory.Spice,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Paprika"
        };
        _dbContext.CanonicalIngredients.AddRange(ingredient1, ingredient2);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRecipeRequest
        {
            CuisineType = "American",
            Ingredients =
            [
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = ingredient1.Id,
                    Quantity = 400m,
                    UnitOfMeasureId = GramUnitOfMeasureId
                },
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = ingredient2.Id,
                    Quantity = 5m,
                    UnitOfMeasureId = GramUnitOfMeasureId
                }
            ],
            Instructions = "Season and grill.",
            ServingCount = 2,
            Title = "Grilled Chicken"
        };

        // Act
        var result = await _sut.CreateRecipeAsync(request);

        // Assert
        var count = await _dbContext.RecipeIngredients.AsNoTracking()
            .CountAsync(ri => ri.RecipeId == result.Id);
        count.Should().Be(2);
    }

    // ── CreateRecipeAsync — container reference in notes ──────────────────────

    // Scenario: CreateRecipeAsync detects container reference in notes
    //   Given a CreateRecipeIngredientRequest with Notes "1 can chopped tomatoes" and UnitOfMeasureId null
    //   When CreateRecipeAsync is called
    //   Then the RecipeIngredient has IsContainerResolved = false

    [Fact]
    public async Task CreateRecipeAsync_IngredientNotesContainCanKeyword_SetsIsContainerResolvedFalse()
    {
        // Arrange
        var canonicalIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Diced Tomatoes"
        };
        _dbContext.CanonicalIngredients.Add(canonicalIngredient);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRecipeRequest
        {
            CuisineType = "Italian",
            Ingredients =
            [
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = canonicalIngredient.Id,
                    Notes = "1 can chopped tomatoes",
                    Quantity = 0m,
                    UnitOfMeasureId = null
                }
            ],
            Instructions = "Simmer the sauce.",
            ServingCount = 4,
            Title = "Tomato Pasta"
        };

        // Act
        var result = await _sut.CreateRecipeAsync(request);

        // Assert
        var ingredient = await _dbContext.RecipeIngredients.AsNoTracking()
            .FirstAsync(ri => ri.RecipeId == result.Id);
        ingredient.IsContainerResolved.Should().BeFalse();
    }

    // ── CreateRecipeAsync — Claude failure ────────────────────────────────────

    // Scenario: CreateRecipeAsync handles Claude dietary classification failure gracefully
    //   Given Claude.ClassifyDietaryTagsAsync throws an exception
    //   When CreateRecipeAsync is called
    //   Then the recipe is still saved
    //   And no exception propagates to the caller

    [Fact]
    public async Task CreateRecipeAsync_ClaudeClassificationThrows_RecipeStillSaved()
    {
        // Arrange
        _claudeServiceMock
            .Setup(c => c.ClassifyDietaryTagsAsync(It.IsAny<Recipe>()))
            .ThrowsAsync(new HttpRequestException("Claude unavailable"));

        var canonicalIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Grain,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Pasta"
        };
        _dbContext.CanonicalIngredients.Add(canonicalIngredient);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRecipeRequest
        {
            CuisineType = "Italian",
            Ingredients =
            [
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = canonicalIngredient.Id,
                    Quantity = 200m,
                    UnitOfMeasureId = GramUnitOfMeasureId
                }
            ],
            Instructions = "Boil pasta.",
            ServingCount = 4,
            Title = "Simple Pasta"
        };

        // Act
        var act = async () => await _sut.CreateRecipeAsync(request);

        // Assert
        await act.Should().NotThrowAsync();
        var saved = await _dbContext.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Title == "Simple Pasta");
        saved.Should().NotBeNull();
    }

    // ── MEP-032: graceful degradation without a Claude API key ────────────────
    //
    // Scenario: CreateRecipeAsync skips dietary classification when no Claude key is configured
    //   Given IClaudeAvailability.IsConfiguredAsync returns false
    //   When CreateRecipeAsync is called
    //   Then Claude.ClassifyDietaryTagsAsync is never invoked
    //   And the recipe is persisted with an empty RecipeDietaryTag collection

    [Fact]
    public async Task CreateRecipeAsync_WithoutClaudeKey_SkipsDietaryClassification()
    {
        // Arrange — flip availability off and make any Claude call fail the test
        _claudeAvailabilityMock
            .Setup(a => a.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var canonicalIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Grain,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Pasta"
        };
        _dbContext.CanonicalIngredients.Add(canonicalIngredient);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRecipeRequest
        {
            CuisineType = "Italian",
            Ingredients =
            [
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = canonicalIngredient.Id,
                    Quantity = 200m,
                    UnitOfMeasureId = GramUnitOfMeasureId
                }
            ],
            Instructions = "Boil pasta.",
            ServingCount = 4,
            Title = "Plain Pasta"
        };

        // Act
        await _sut.CreateRecipeAsync(request);

        // Assert — Claude classification never invoked
        _claudeServiceMock.Verify(
            c => c.ClassifyDietaryTagsAsync(It.IsAny<Recipe>()),
            Times.Never);

        // And recipe saved without any dietary tags
        var saved = await _dbContext.Recipes.AsNoTracking()
            .FirstAsync(r => r.Title == "Plain Pasta");
        var tagCount = await _dbContext.RecipeDietaryTags.CountAsync(t => t.RecipeId == saved.Id);
        tagCount.Should().Be(0);
    }

    // ── GetAllLocalRecipesAsync — empty database ───────────────────────────────

    // Scenario: GetAllLocalRecipesAsync returns empty list when no recipes exist
    //   Given no recipes in the database
    //   When GetAllLocalRecipesAsync is called
    //   Then an empty list is returned

    [Fact]
    public async Task GetAllLocalRecipesAsync_NoRecipes_ReturnsEmptyList()
    {
        // Arrange — nothing seeded beyond reference units of measure

        // Act
        var result = await _sut.GetAllLocalRecipesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ── GetAllLocalRecipesAsync — ordering ────────────────────────────────────

    // Scenario: GetAllLocalRecipesAsync returns recipes ordered by title
    //   Given recipes "Zucchini Soup" and "Apple Cake" exist
    //   When GetAllLocalRecipesAsync is called
    //   Then results are ordered alphabetically: "Apple Cake" first

    [Fact]
    public async Task GetAllLocalRecipesAsync_MultipleRecipes_OrderedByTitleAscending()
    {
        // Arrange
        _dbContext.Recipes.AddRange(
            new Recipe
            {
                CuisineType = "American",
                Id = Guid.NewGuid(),
                Instructions = "Cook.",
                ServingCount = 4,
                Title = "Zucchini Soup"
            },
            new Recipe
            {
                CuisineType = "French",
                Id = Guid.NewGuid(),
                Instructions = "Bake.",
                ServingCount = 8,
                Title = "Apple Cake"
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllLocalRecipesAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Apple Cake");
        result[1].Title.Should().Be("Zucchini Soup");
    }

    // ── GetAllLocalRecipesAsync — unresolved count ────────────────────────────

    // Scenario: GetAllLocalRecipesAsync returns correct unresolved count
    //   Given a recipe with one resolved and one unresolved ingredient
    //   When GetAllLocalRecipesAsync is called
    //   Then UnresolvedCount equals 1

    [Fact]
    public async Task GetAllLocalRecipesAsync_RecipeWithUnresolvedIngredients_ReturnsCorrectUnresolvedCount()
    {
        // Arrange
        var canonical = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = EachUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Canned Beans"
        };
        _dbContext.CanonicalIngredients.Add(canonical);

        var recipe = new Recipe
        {
            CuisineType = "Mexican",
            Id = Guid.NewGuid(),
            Instructions = "Mix.",
            ServingCount = 4,
            Title = "Bean Soup"
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.RecipeIngredients.AddRange(
            new RecipeIngredient
            {
                CanonicalIngredientId = canonical.Id,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = 200m,
                RecipeId = recipe.Id,
                UnitOfMeasureId = GramUnitOfMeasureId
            },
            new RecipeIngredient
            {
                CanonicalIngredientId = canonical.Id,
                Id = Guid.NewGuid(),
                IsContainerResolved = false,
                Notes = "1 can",
                Quantity = 0m,
                RecipeId = recipe.Id,
                UnitOfMeasureId = null
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllLocalRecipesAsync();

        // Assert
        var dto = result.Should().ContainSingle().Subject;
        dto.UnresolvedCount.Should().Be(1);
    }

    // ── GetRecipeDetailAsync — not found ──────────────────────────────────────

    // Scenario: GetRecipeDetailAsync returns null when recipe not found
    //   Given no recipe with a particular ID
    //   When GetRecipeDetailAsync is called
    //   Then null is returned

    [Fact]
    public async Task GetRecipeDetailAsync_RecipeNotFound_ReturnsNull()
    {
        // Arrange — non-existent ID
        var missingId = Guid.NewGuid();

        // Act
        var result = await _sut.GetRecipeDetailAsync(missingId);

        // Assert
        result.Should().BeNull();
    }

    // ── GetRecipeDetailAsync — found ──────────────────────────────────────────

    // Scenario: GetRecipeDetailAsync returns detail when recipe exists
    //   Given a recipe "Beef Stew" with cuisine "French"
    //   When GetRecipeDetailAsync is called with its ID
    //   Then the returned detail has Title "Beef Stew" and CuisineType "French"

    [Fact]
    public async Task GetRecipeDetailAsync_ExistingRecipe_ReturnsCorrectDetail()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        _dbContext.Recipes.Add(new Recipe
        {
            CuisineType = "French",
            Id = recipeId,
            Instructions = "Brown the beef and simmer.",
            ServingCount = 6,
            Title = "Beef Stew"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecipeDetailAsync(recipeId);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Beef Stew");
        result.CuisineType.Should().Be("French");
    }

    // ── ImportByIdAsync — result metadata ─────────────────────────────────────

    [Fact]
    public async Task ImportByIdAsync_ValidMeal_ResultRecipeIdMatchesSavedRecord()
    {
        // Arrange
        var meal = BuildMeal();
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        var exists = await _dbContext.Recipes.AsNoTracking()
            .AnyAsync(r => r.Id == result.RecipeId);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ImportByIdAsync_ValidMeal_ResultTitleMatchesMealName()
    {
        // Arrange
        var meal = BuildMeal(mealName: "Beef Bourguignon");
        _theMealDbClientMock.Setup(c => c.GetByIdAsync("52772", It.IsAny<CancellationToken>()))
                            .ReturnsAsync(meal);

        // Act
        var result = await _sut.ImportByIdAsync("52772");

        // Assert
        result.Title.Should().Be("Beef Bourguignon");
    }
}
