// Feature: Recipe Import Service
//
// Scenario: CreateRecipeAsync creates recipe with correct title
//   Given a CreateRecipeRequest with Title "Homemade Tacos"
//   When CreateRecipeAsync is called
//   Then the saved Recipe has Title "Homemade Tacos"
//
// Scenario: CreateRecipeAsync creates recipe ingredients
//   Given a CreateRecipeRequest with two ingredients
//   When CreateRecipeAsync is called
//   Then the saved Recipe has exactly two RecipeIngredients
//
// Scenario: CreateRecipeAsync detects container reference in notes
//   Given a CreateRecipeIngredientRequest with Notes "1 can chopped tomatoes" and UnitOfMeasureId null
//   When CreateRecipeAsync is called
//   Then the RecipeIngredient has IsContainerResolved = false
//
// Scenario: CreateRecipeAsync handles Claude dietary classification failure gracefully
//   Given Claude.ClassifyDietaryTagsAsync throws an exception
//   When CreateRecipeAsync is called
//   Then the recipe is still saved and no exception propagates
//
// Scenario: CreateRecipeAsync skips dietary classification when no Claude key is configured (MEP-032)
//   Given IClaudeAvailability.IsConfiguredAsync returns false
//   When CreateRecipeAsync is called
//   Then Claude.ClassifyDietaryTagsAsync is never invoked and the recipe has no dietary tags
//
// Scenario: GetAllLocalRecipesAsync returns empty list when no recipes exist
// Scenario: GetAllLocalRecipesAsync returns recipes ordered by title ascending
// Scenario: GetAllLocalRecipesAsync returns correct unresolved count
// Scenario: GetRecipeDetailAsync returns null when recipe not found
// Scenario: GetRecipeDetailAsync returns full detail for an existing recipe

using FluentAssertions;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
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

        SeedReferenceData();

        _claudeAvailabilityMock
            .Setup(a => a.IsConfiguredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new RecipeImportService(
            _claudeAvailabilityMock.Object,
            _claudeServiceMock.Object,
            _dbContext,
            NullLogger<RecipeImportService>.Instance);
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

    // ── CreateRecipeAsync ─────────────────────────────────────────────────────

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

    // ── GetAllLocalRecipesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetAllLocalRecipesAsync_NoRecipes_ReturnsEmptyList()
    {
        // Arrange — nothing seeded beyond reference units of measure

        // Act
        var result = await _sut.GetAllLocalRecipesAsync();

        // Assert
        result.Should().BeEmpty();
    }

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

    // ── GetRecipeDetailAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetRecipeDetailAsync_RecipeNotFound_ReturnsNull()
    {
        // Act
        var result = await _sut.GetRecipeDetailAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

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
}
