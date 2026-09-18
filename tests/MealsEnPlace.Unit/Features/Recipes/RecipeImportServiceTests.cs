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
// Scenario: CreateRecipeAsync increments RecipeReferenceCount for each referenced canonical ingredient
//   Given a recipe with two RecipeIngredient rows for the same canonical ingredient and one row for a second
//   When CreateRecipeAsync is called
//   Then the first canonical ingredient's RecipeReferenceCount is incremented by 2
//   And the second canonical ingredient's RecipeReferenceCount is incremented by 1
//
// Scenario: CreateRecipeAsync with zero ingredients does not change any RecipeReferenceCount
//   Given canonical ingredients exist with known RecipeReferenceCounts
//   And the CreateRecipeRequest has an empty Ingredients list
//   When CreateRecipeAsync is called
//   Then no canonical ingredient's RecipeReferenceCount is changed
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
// Scenario: GetPagedLocalRecipesAsync returns empty Items when no recipes exist
// Scenario: GetPagedLocalRecipesAsync returns recipes ordered by title ascending
// Scenario: GetPagedLocalRecipesAsync returns correct unresolved count
// Scenario: GetPagedLocalRecipesAsync returns correct TotalCount
// Scenario: GetPagedLocalRecipesAsync clamps page below 1 to 1
// Scenario: GetPagedLocalRecipesAsync clamps pageSize above MaxPageSize to MaxPageSize
// Scenario: GetPagedLocalRecipesAsync clamps pageSize below 1 to 1
// Scenario: GetPagedLocalRecipesAsync respects Skip for page 2
//
// Scenario: Title search returns only recipes whose title contains the search term
//   Given recipes "Chicken Tikka Masala" and "Beef Stew" exist
//   When GetPagedLocalRecipesAsync is called with TitleSearch "tikka"
//   Then only "Chicken Tikka Masala" is returned
//
// Scenario: Title search is case-insensitive
//   Given a recipe titled "Chicken Tikka Masala" exists
//   When GetPagedLocalRecipesAsync is called with TitleSearch "TIKKA MASALA"
//   Then the recipe is returned
//
// Scenario: Title search with no matching recipe returns empty Items
//   Given no recipe title contains "xyznonexistent123"
//   When GetPagedLocalRecipesAsync is called with TitleSearch "xyznonexistent123"
//   Then Items is empty
//
// Scenario: Title search with no matching recipe returns TotalCount of zero
//   Given no recipe title contains "xyznonexistent123"
//   When GetPagedLocalRecipesAsync is called with TitleSearch "xyznonexistent123"
//   Then TotalCount is 0
//
// Scenario: Ingredient search returns only recipes that contain the matching ingredient
//   Given "Pasta Primavera" (contains "Broccoli") and "Beef Stew" (no broccoli) exist
//   When GetPagedLocalRecipesAsync is called with IngredientSearch "broccoli"
//   Then only "Pasta Primavera" is returned
//
// Scenario: Ingredient search excludes recipes without the matching ingredient
//   Given "Beef Stew" contains no ingredient named "broccoli"
//   When GetPagedLocalRecipesAsync is called with IngredientSearch "broccoli"
//   Then "Beef Stew" is not in the result
//
// Scenario: Ingredient search is case-insensitive
//   Given a recipe contains an ingredient named "Chicken Thigh"
//   When GetPagedLocalRecipesAsync is called with IngredientSearch "CHICKEN THIGH"
//   Then the recipe is returned
//
// Scenario: Title search and dietary-tag filter combine with AND semantics
//   Given "Vegetarian Pasta" (Vegetarian, title contains "pasta") and "Chicken Pasta" (Carnivore) exist
//   When GetPagedLocalRecipesAsync is called with TitleSearch "pasta" and DietaryTags [Vegetarian]
//   Then only "Vegetarian Pasta" is returned
//
// Scenario: Multiple dietary-tag filters all must match
//   Given "Vegan GF Salad" (Vegan + GlutenFree), "Vegan Pasta" (Vegan only), "GF Steak" (GlutenFree only) exist
//   When GetPagedLocalRecipesAsync is called with DietaryTags [Vegan, GlutenFree]
//   Then only "Vegan GF Salad" is returned
//
// Scenario: Empty search (no TitleSearch, IngredientSearch, or DietaryTags) returns all recipes
//   Given 3 recipes exist
//   When GetPagedLocalRecipesAsync is called with all search fields null or empty
//   Then all 3 recipes are returned
//
// Scenario: TotalCount reflects the filtered count, not the full catalog count
//   Given 5 recipes exist, only 2 contain "pasta" in the title
//   When GetPagedLocalRecipesAsync is called with TitleSearch "pasta" and pageSize 25
//   Then TotalCount is 2
//
// Scenario: Pagination on a filtered result set uses filtered count for page math
//   Given 3 recipes contain "pasta" in the title
//   When GetPagedLocalRecipesAsync is called with TitleSearch "pasta", page 2, and pageSize 1
//   Then Items contains the second alphabetical matching recipe
//   And TotalCount is 3
//   And TotalPages is 3
//
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
    public async Task CreateRecipeAsync_TwoRowsForSameCanonicalAndOneForAnother_IncrementsCountsCorrectly()
    {
        // Arrange — one canonical ingredient appears twice in the recipe; a second appears once.
        // Both start at RecipeReferenceCount = 0 (the default).
        var doubleIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Grain,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Egg"
        };
        var singleIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Spice,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Salt"
        };
        _dbContext.CanonicalIngredients.AddRange(doubleIngredient, singleIngredient);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRecipeRequest
        {
            CuisineType = "American",
            Ingredients =
            [
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = doubleIngredient.Id,
                    Notes = "egg in batter",
                    Quantity = 2m,
                    UnitOfMeasureId = EachUnitOfMeasureId
                },
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = doubleIngredient.Id,
                    Notes = "egg wash",
                    Quantity = 1m,
                    UnitOfMeasureId = EachUnitOfMeasureId
                },
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = singleIngredient.Id,
                    Quantity = 5m,
                    UnitOfMeasureId = GramUnitOfMeasureId
                }
            ],
            Instructions = "Combine and bake.",
            ServingCount = 4,
            Title = "Egg Bake"
        };

        // Act
        await _sut.CreateRecipeAsync(request);

        // Assert — double-referenced canonical gets +2; single-referenced gets +1
        var updatedDouble = await _dbContext.CanonicalIngredients
            .AsNoTracking()
            .FirstAsync(c => c.Id == doubleIngredient.Id);
        updatedDouble.RecipeReferenceCount.Should().Be(2);

        var updatedSingle = await _dbContext.CanonicalIngredients
            .AsNoTracking()
            .FirstAsync(c => c.Id == singleIngredient.Id);
        updatedSingle.RecipeReferenceCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateRecipeAsync_ZeroIngredients_DoesNotChangeAnyRecipeReferenceCount()
    {
        // Arrange — seed a canonical ingredient with a known starting count
        var existingIngredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Basil",
            RecipeReferenceCount = 42
        };
        _dbContext.CanonicalIngredients.Add(existingIngredient);
        await _dbContext.SaveChangesAsync();

        // A recipe with no ingredients — Ingredients list is intentionally empty
        var request = new CreateRecipeRequest
        {
            CuisineType = "Italian",
            Ingredients = [],
            Instructions = "No ingredients required.",
            ServingCount = 2,
            Title = "Empty Recipe"
        };

        // Act
        await _sut.CreateRecipeAsync(request);

        // Assert — count must remain at 42; no increments from a zero-ingredient recipe
        var reloaded = await _dbContext.CanonicalIngredients
            .AsNoTracking()
            .FirstAsync(c => c.Id == existingIngredient.Id);
        reloaded.RecipeReferenceCount.Should().Be(42);
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

    // ── GetPagedLocalRecipesAsync — empty catalog ─────────────────────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_NoRecipes_ReturnsEmptyItems()
    {
        // Arrange — nothing seeded beyond reference units of measure

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, null));

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ── GetPagedLocalRecipesAsync — ordering ──────────────────────────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_MultipleRecipes_OrderedByTitleAscending()
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
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, null));

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items[0].Title.Should().Be("Apple Cake");
        result.Items[1].Title.Should().Be("Zucchini Soup");
    }

    // ── GetPagedLocalRecipesAsync — unresolved count ──────────────────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_RecipeWithUnresolvedIngredients_ReturnsCorrectUnresolvedCount()
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
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, null));

        // Assert
        var dto = result.Items.Should().ContainSingle().Subject;
        dto.UnresolvedCount.Should().Be(1);
    }

    // ── GetPagedLocalRecipesAsync — TotalCount metadata ──────────────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_MultipleRecipes_TotalCountReflectsFullCatalog()
    {
        // Arrange — seed 3 recipes, request only 2 per page
        _dbContext.Recipes.AddRange(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Alpha" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Beta" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Gamma" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 2, null));

        // Assert
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(2);
        result.TotalPages.Should().Be(2);
    }

    // ── GetPagedLocalRecipesAsync — page boundary: page 2 skips page 1 items ──

    [Fact]
    public async Task GetPagedLocalRecipesAsync_Page2WithPageSize1_ReturnsSecondRecipe()
    {
        // Arrange
        _dbContext.Recipes.AddRange(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Alpha" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Beta" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 2, 1, null));

        // Assert
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Beta");
        result.Page.Should().Be(2);
    }

    // ── GetPagedLocalRecipesAsync — clamping ──────────────────────────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_PageBelowOne_ClampsToOne()
    {
        // Arrange — empty catalog is fine; we care about the returned Page field
        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, -5, 25, null));

        // Assert
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_PageSizeAboveMax_ClampsToMaxPageSize()
    {
        // Arrange — seed 1 recipe so we can observe the clamped Items count
        _dbContext.Recipes.Add(new Recipe
        {
            CuisineType = string.Empty,
            Id = Guid.NewGuid(),
            Instructions = string.Empty,
            ServingCount = 1,
            Title = "Only Recipe"
        });
        await _dbContext.SaveChangesAsync();

        // Act — request 10,000 per page
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 10_000, null));

        // Assert
        result.PageSize.Should().Be(RecipeImportService.MaxPageSize);
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_PageSizeBelowOne_ClampsToOne()
    {
        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 0, null));

        // Assert
        result.PageSize.Should().Be(1);
    }

    // ── GetPagedLocalRecipesAsync — title search ──────────────────────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_TitleSearch_ReturnsOnlyRecipesWhoseTitleContainsSearchTerm()
    {
        // Arrange
        _dbContext.Recipes.AddRange(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Chicken Tikka Masala" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Beef Stew" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, "tikka"));

        // Assert
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Chicken Tikka Masala");
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_TitleSearch_IsCaseInsensitive()
    {
        // Arrange
        _dbContext.Recipes.Add(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Chicken Tikka Masala" });
        await _dbContext.SaveChangesAsync();

        // Act — search in all-caps
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, "TIKKA MASALA"));

        // Assert
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Chicken Tikka Masala");
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_TitleSearch_NoMatch_ReturnsEmptyItems()
    {
        // Arrange
        _dbContext.Recipes.Add(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Beef Stew" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, "xyznonexistent123"));

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_TitleSearch_NoMatch_TotalCountIsZero()
    {
        // Arrange
        _dbContext.Recipes.Add(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Beef Stew" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, "xyznonexistent123"));

        // Assert
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_TitleSearch_TotalCountReflectsFilteredCount()
    {
        // Arrange — 5 recipes total; only 2 contain "pasta" in the title
        _dbContext.Recipes.AddRange(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Pasta Primavera" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Chicken Pasta Bake" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Beef Stew" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Apple Crumble" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Lentil Soup" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, "pasta"));

        // Assert — filtered count, not full catalog
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_TitleSearch_PaginationWorksOnFilteredResultSet()
    {
        // Arrange — 3 recipes contain "pasta"; request page 2 of pageSize 1 against filtered results
        _dbContext.Recipes.AddRange(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Pasta Arrabiata" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Pasta Carbonara" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Pasta Primavera" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Beef Stew" });
        await _dbContext.SaveChangesAsync();

        // Act — page 2 of the filtered (pasta-only) result set
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 2, 1, "pasta"));

        // Assert — second alphabetical pasta recipe, filtered TotalCount and TotalPages
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Pasta Carbonara");
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(3);
    }

    // ── GetPagedLocalRecipesAsync — ingredient search ─────────────────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_IngredientSearch_ReturnsOnlyRecipesWithMatchingIngredient()
    {
        // Arrange
        var broccoli = new CanonicalIngredient
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Broccoli"
        };
        var beef = new CanonicalIngredient
        {
            Category = IngredientCategory.Protein,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Beef"
        };
        _dbContext.CanonicalIngredients.AddRange(broccoli, beef);

        var pastaPrimavera = new Recipe
        {
            CuisineType = string.Empty,
            Id = Guid.NewGuid(),
            Instructions = string.Empty,
            ServingCount = 2,
            Title = "Pasta Primavera"
        };
        var beefStew = new Recipe
        {
            CuisineType = string.Empty,
            Id = Guid.NewGuid(),
            Instructions = string.Empty,
            ServingCount = 4,
            Title = "Beef Stew"
        };
        _dbContext.Recipes.AddRange(pastaPrimavera, beefStew);

        _dbContext.RecipeIngredients.AddRange(
            new RecipeIngredient
            {
                CanonicalIngredientId = broccoli.Id,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = 200m,
                RecipeId = pastaPrimavera.Id,
                UnitOfMeasureId = GramUnitOfMeasureId
            },
            new RecipeIngredient
            {
                CanonicalIngredientId = beef.Id,
                Id = Guid.NewGuid(),
                IsContainerResolved = true,
                Quantity = 500m,
                RecipeId = beefStew.Id,
                UnitOfMeasureId = GramUnitOfMeasureId
            });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], "broccoli", 1, 25, null));

        // Assert
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Pasta Primavera");
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_IngredientSearch_ExcludesRecipesWithoutMatchingIngredient()
    {
        // Arrange
        var beef = new CanonicalIngredient
        {
            Category = IngredientCategory.Protein,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Beef"
        };
        _dbContext.CanonicalIngredients.Add(beef);

        var beefStew = new Recipe
        {
            CuisineType = string.Empty,
            Id = Guid.NewGuid(),
            Instructions = string.Empty,
            ServingCount = 4,
            Title = "Beef Stew"
        };
        _dbContext.Recipes.Add(beefStew);
        _dbContext.RecipeIngredients.Add(new RecipeIngredient
        {
            CanonicalIngredientId = beef.Id,
            Id = Guid.NewGuid(),
            IsContainerResolved = true,
            Quantity = 500m,
            RecipeId = beefStew.Id,
            UnitOfMeasureId = GramUnitOfMeasureId
        });
        await _dbContext.SaveChangesAsync();

        // Act — searching for "broccoli" which Beef Stew does not contain
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], "broccoli", 1, 25, null));

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_IngredientSearch_IsCaseInsensitive()
    {
        // Arrange
        var chickenThigh = new CanonicalIngredient
        {
            Category = IngredientCategory.Protein,
            DefaultUnitOfMeasureId = GramUnitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = "Chicken Thigh"
        };
        _dbContext.CanonicalIngredients.Add(chickenThigh);

        var recipe = new Recipe
        {
            CuisineType = string.Empty,
            Id = Guid.NewGuid(),
            Instructions = string.Empty,
            ServingCount = 4,
            Title = "Roasted Chicken"
        };
        _dbContext.Recipes.Add(recipe);
        _dbContext.RecipeIngredients.Add(new RecipeIngredient
        {
            CanonicalIngredientId = chickenThigh.Id,
            Id = Guid.NewGuid(),
            IsContainerResolved = true,
            Quantity = 600m,
            RecipeId = recipe.Id,
            UnitOfMeasureId = GramUnitOfMeasureId
        });
        await _dbContext.SaveChangesAsync();

        // Act — search in all-caps
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], "CHICKEN THIGH", 1, 25, null));

        // Assert
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Roasted Chicken");
    }

    // ── GetPagedLocalRecipesAsync — combined filters (AND semantics) ──────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_TitleSearchAndDietaryTag_AppliesAndSemantics()
    {
        // Arrange — "Vegetarian Pasta" matches both title "pasta" and tag Vegetarian;
        // "Chicken Pasta" matches title but not tag; "Vegetarian Soup" matches tag but not title
        var vegPasta = new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Vegetarian Pasta" };
        var chickenPasta = new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Chicken Pasta" };
        var vegSoup = new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Vegetarian Soup" };
        _dbContext.Recipes.AddRange(vegPasta, chickenPasta, vegSoup);
        _dbContext.RecipeDietaryTags.AddRange(
            new RecipeDietaryTag { Id = Guid.NewGuid(), RecipeId = vegPasta.Id, Tag = DietaryTag.Vegetarian },
            new RecipeDietaryTag { Id = Guid.NewGuid(), RecipeId = vegSoup.Id, Tag = DietaryTag.Vegetarian });
        await _dbContext.SaveChangesAsync();

        // Act — title contains "pasta" AND has Vegetarian tag
        var result = await _sut.GetPagedLocalRecipesAsync(
            new RecipeSearchQuery([DietaryTag.Vegetarian], null, 1, 25, "pasta"));

        // Assert — only the recipe matching both predicates is returned
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Vegetarian Pasta");
    }

    [Fact]
    public async Task GetPagedLocalRecipesAsync_MultipleDietaryTagFilters_AllMustBePresent()
    {
        // Arrange — only "Vegan GF Salad" carries both Vegan and GlutenFree
        var veganGfSalad = new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Vegan GF Salad" };
        var veganPasta = new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "Vegan Pasta" };
        var gfSteak = new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 2, Title = "GF Steak" };
        _dbContext.Recipes.AddRange(veganGfSalad, veganPasta, gfSteak);
        _dbContext.RecipeDietaryTags.AddRange(
            new RecipeDietaryTag { Id = Guid.NewGuid(), RecipeId = veganGfSalad.Id, Tag = DietaryTag.Vegan },
            new RecipeDietaryTag { Id = Guid.NewGuid(), RecipeId = veganGfSalad.Id, Tag = DietaryTag.GlutenFree },
            new RecipeDietaryTag { Id = Guid.NewGuid(), RecipeId = veganPasta.Id, Tag = DietaryTag.Vegan },
            new RecipeDietaryTag { Id = Guid.NewGuid(), RecipeId = gfSteak.Id, Tag = DietaryTag.GlutenFree });
        await _dbContext.SaveChangesAsync();

        // Act — require BOTH Vegan AND GlutenFree
        var result = await _sut.GetPagedLocalRecipesAsync(
            new RecipeSearchQuery([DietaryTag.Vegan, DietaryTag.GlutenFree], null, 1, 25, null));

        // Assert — only the recipe carrying both tags is returned
        result.Items.Should().ContainSingle()
            .Which.Title.Should().Be("Vegan GF Salad");
    }

    // ── GetPagedLocalRecipesAsync — empty search (MEP-043 parity) ────────────

    [Fact]
    public async Task GetPagedLocalRecipesAsync_NoSearchTermsOrDietaryTags_ReturnsAllRecipesUnfiltered()
    {
        // Arrange
        _dbContext.Recipes.AddRange(
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Alpha" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Beta" },
            new Recipe { CuisineType = string.Empty, Id = Guid.NewGuid(), Instructions = string.Empty, ServingCount = 1, Title = "Gamma" });
        await _dbContext.SaveChangesAsync();

        // Act — all search fields null or empty, identical to MEP-043 parity
        var result = await _sut.GetPagedLocalRecipesAsync(new RecipeSearchQuery([], null, 1, 25, null));

        // Assert — all 3 recipes returned
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
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
