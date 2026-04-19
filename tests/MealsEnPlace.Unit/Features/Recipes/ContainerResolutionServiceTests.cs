// Feature: Container Reference Resolution — Recipe Side
//
// Scenario: Resolve an unresolved ingredient — sets IsContainerResolved = true
//   Given a recipe with one unresolved RecipeIngredient
//   And a valid ResolveContainerRequest with positive quantity and a known UnitOfMeasureId
//   When ResolveAsync is called
//   Then the result IsSuccess is true
//   And the returned ingredient has IsContainerResolved = true
//
// Scenario: Resolve an unresolved ingredient — stores correct quantity and unit of measure
//   Given a recipe with one unresolved RecipeIngredient
//   And a ResolveContainerRequest with Quantity 14.5 and a known oz UnitOfMeasureId
//   When ResolveAsync is called
//   Then the resolved ingredient Quantity equals 14.5
//   And the resolved ingredient UnitOfMeasureId equals the declared oz UnitOfMeasureId
//
// Scenario: Resolve an unresolved ingredient — Notes field is preserved unchanged
//   Given a RecipeIngredient with Notes "1 can chopped tomatoes" and IsContainerResolved = false
//   When ResolveAsync is called with a valid declaration
//   Then the resolved ingredient Notes still equals "1 can chopped tomatoes"
//
// Scenario: Resolve with invalid unit of measure id — returns validation error
//   Given a ResolveContainerRequest whose UnitOfMeasureId does not exist in the database
//   When ResolveAsync is called
//   Then the result IsValidationError is true
//   And the result ErrorMessage is not empty
//
// Scenario: Resolve with zero quantity — returns validation error
//   Given a ResolveContainerRequest with Quantity = 0
//   When ResolveAsync is called
//   Then the result IsValidationError is true
//   And the result ErrorMessage is not empty
//
// Scenario: Recipe not found — returns RecipeNotFound
//   Given a recipeId that does not exist in the database
//   When ResolveAsync is called
//   Then the result IsRecipeNotFound is true
//
// Scenario: Ingredient not found — returns IngredientNotFound
//   Given a valid recipe and a valid unit of measure
//   But an ingredientId that does not belong to that recipe
//   When ResolveAsync is called
//   Then the result IsIngredientNotFound is true
//
// Scenario: GetUnresolvedRecipesAsync — returns only recipes with unresolved ingredients
//   Given two recipes: one with an unresolved ingredient, one with all resolved ingredients
//   When GetUnresolvedRecipesAsync is called
//   Then only the recipe with the unresolved ingredient is returned
//
// Scenario: GetUnresolvedRecipesAsync — excludes fully resolved recipes
//   Given a recipe where all RecipeIngredients have IsContainerResolved = true
//   When GetUnresolvedRecipesAsync is called
//   Then the fully resolved recipe is not in the result
//
// Scenario: GetUnresolvedIngredientsAsync — returns only unresolved ingredients for a recipe
//   Given a recipe with one resolved and one unresolved RecipeIngredient
//   When GetUnresolvedIngredientsAsync is called for that recipe
//   Then only the unresolved ingredient is returned
//
// Scenario: Partially resolved recipe (3 of 4 resolved) still appears in unresolved list
//   Given a recipe with 4 RecipeIngredients where 3 are resolved and 1 is not
//   When GetUnresolvedRecipesAsync is called
//   Then the recipe appears in the result
//   And the single unresolved ingredient is listed (critical edge case #5)
//
// Scenario: GetUnresolvedIngredientsAsync — returns null when recipe does not exist
//   Given a recipeId that does not exist in the database
//   When GetUnresolvedIngredientsAsync is called
//   Then the result is null

using FluentAssertions;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.Recipes;

public class ContainerResolutionServiceTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly ContainerResolutionService _sut;

    // Stable IDs for seeded entities
    private static readonly Guid CanonicalIngredientId1 = Guid.NewGuid();
    private static readonly Guid CanonicalIngredientId2 = Guid.NewGuid();
    private static readonly Guid CanonicalIngredientId3 = Guid.NewGuid();
    private static readonly Guid CanonicalIngredientId4 = Guid.NewGuid();
    private static readonly Guid OzUnitOfMeasureId = Guid.NewGuid();
    private static readonly Guid GramsUnitOfMeasureId = Guid.NewGuid();

    public ContainerResolutionServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MealsEnPlaceDbContext(options);
        _sut = new ContainerResolutionService(_dbContext);

        SeedReferenceData();
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private void SeedReferenceData()
    {
        _dbContext.UnitsOfMeasure.AddRange(
            new UnitOfMeasure
            {
                Abbreviation = "g",
                ConversionFactor = 1m,
                Id = GramsUnitOfMeasureId,
                Name = "Gram",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "oz",
                ConversionFactor = 28.3495m,
                Id = OzUnitOfMeasureId,
                Name = "Ounce",
                UnitOfMeasureType = UnitOfMeasureType.Weight
            });

        _dbContext.CanonicalIngredients.AddRange(
            BuildCanonicalIngredient(CanonicalIngredientId1, "Diced Tomatoes"),
            BuildCanonicalIngredient(CanonicalIngredientId2, "Marinara Sauce"),
            BuildCanonicalIngredient(CanonicalIngredientId3, "Kidney Beans"),
            BuildCanonicalIngredient(CanonicalIngredientId4, "Chicken Stock"));

        _dbContext.SaveChanges();
    }

    private static CanonicalIngredient BuildCanonicalIngredient(Guid id, string name) =>
        new()
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = GramsUnitOfMeasureId,
            Id = id,
            Name = name
        };

    private Recipe SeedRecipeWithIngredients(
        string title,
        IEnumerable<RecipeIngredient> ingredients)
    {
        var recipe = new Recipe
        {
            CuisineType = "Italian",
            Id = Guid.NewGuid(),
            Instructions = "Cook it.",
            ServingCount = 4,
            Title = title
        };

        _dbContext.Recipes.Add(recipe);
        _dbContext.SaveChanges();

        foreach (var ingredient in ingredients)
        {
            ingredient.RecipeId = recipe.Id;
            _dbContext.RecipeIngredients.Add(ingredient);
        }

        _dbContext.SaveChanges();
        return recipe;
    }

    private static RecipeIngredient BuildUnresolvedIngredient(
        Guid canonicalIngredientId,
        string notes) =>
        new()
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            IsContainerResolved = false,
            Notes = notes,
            Quantity = 0m,
            UnitOfMeasureId = null
        };

    private static RecipeIngredient BuildResolvedIngredient(
        Guid canonicalIngredientId,
        decimal quantity = 250m) =>
        new()
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            IsContainerResolved = true,
            Notes = null,
            Quantity = quantity,
            UnitOfMeasureId = GramsUnitOfMeasureId
        };

    // ── GetUnresolvedIngredientsAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetUnresolvedIngredientsAsync_RecipeDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentRecipeId = Guid.NewGuid();

        // Act
        var result = await _sut.GetUnresolvedIngredientsAsync(nonExistentRecipeId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUnresolvedIngredientsAsync_RecipeWithMixedIngredients_ReturnsOnlyUnresolvedOnes()
    {
        // Arrange
        var resolved = BuildResolvedIngredient(CanonicalIngredientId1);
        var unresolved = BuildUnresolvedIngredient(CanonicalIngredientId2, "1 jar marinara sauce");
        var recipe = SeedRecipeWithIngredients("Pasta Bake", [resolved, unresolved]);

        // Act
        var result = await _sut.GetUnresolvedIngredientsAsync(recipe.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result.Single().Id.Should().Be(unresolved.Id);
    }

    [Fact]
    public async Task GetUnresolvedIngredientsAsync_FullyResolvedRecipe_ReturnsEmptyList()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Simple Pasta", [
            BuildResolvedIngredient(CanonicalIngredientId1),
            BuildResolvedIngredient(CanonicalIngredientId2)
        ]);

        // Act
        var result = await _sut.GetUnresolvedIngredientsAsync(recipe.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    // ── GetUnresolvedRecipesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetUnresolvedRecipesAsync_NoRecipesExist_ReturnsEmptyList()
    {
        // Arrange — no seed data added beyond reference entities

        // Act
        var result = await _sut.GetUnresolvedRecipesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnresolvedRecipesAsync_OneUnresolvedOneResolved_ReturnsOnlyUnresolvedRecipe()
    {
        // Arrange
        SeedRecipeWithIngredients("Fully Resolved Pasta", [
            BuildResolvedIngredient(CanonicalIngredientId1),
            BuildResolvedIngredient(CanonicalIngredientId2)
        ]);

        var unresolvedRecipe = SeedRecipeWithIngredients("Chili Con Carne", [
            BuildResolvedIngredient(CanonicalIngredientId1),
            BuildUnresolvedIngredient(CanonicalIngredientId3, "1 can kidney beans")
        ]);

        // Act
        var result = await _sut.GetUnresolvedRecipesAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(unresolvedRecipe.Id);
    }

    [Fact]
    public async Task GetUnresolvedRecipesAsync_FullyResolvedRecipe_IsExcludedFromResult()
    {
        // Arrange
        SeedRecipeWithIngredients("Fully Resolved Pasta", [
            BuildResolvedIngredient(CanonicalIngredientId1),
            BuildResolvedIngredient(CanonicalIngredientId2)
        ]);

        // Act
        var result = await _sut.GetUnresolvedRecipesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnresolvedRecipesAsync_PartiallyResolvedRecipe_StillAppearsInResult()
    {
        // Arrange — 3 of 4 ingredients resolved; the 4th is still an unresolved container reference
        // This is critical edge case #5: partial resolution never grants partial participation.
        var partiallyResolvedRecipe = SeedRecipeWithIngredients("Chicken Soup", [
            BuildResolvedIngredient(CanonicalIngredientId1),
            BuildResolvedIngredient(CanonicalIngredientId2),
            BuildResolvedIngredient(CanonicalIngredientId3),
            BuildUnresolvedIngredient(CanonicalIngredientId4, "1 carton chicken stock")
        ]);

        // Act
        var result = await _sut.GetUnresolvedRecipesAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(partiallyResolvedRecipe.Id);
    }

    [Fact]
    public async Task GetUnresolvedRecipesAsync_PartiallyResolvedRecipe_ListsOnlyTheOneUnresolvedIngredient()
    {
        // Arrange — 3 of 4 resolved; verify exactly the single unresolved ingredient is reported
        var unresolvedIngredient = BuildUnresolvedIngredient(CanonicalIngredientId4, "1 carton chicken stock");
        SeedRecipeWithIngredients("Chicken Soup", [
            BuildResolvedIngredient(CanonicalIngredientId1),
            BuildResolvedIngredient(CanonicalIngredientId2),
            BuildResolvedIngredient(CanonicalIngredientId3),
            unresolvedIngredient
        ]);

        // Act
        var result = await _sut.GetUnresolvedRecipesAsync();

        // Assert
        var recipe = result.Single();
        var unresolvedOnes = recipe.RecipeIngredients.Where(ri => !ri.IsContainerResolved).ToList();
        unresolvedOnes.Should().HaveCount(1);
        unresolvedOnes.Single().Id.Should().Be(unresolvedIngredient.Id);
    }

    [Fact]
    public async Task GetUnresolvedRecipesAsync_MultipleUnresolvedRecipes_ReturnsAllOfThem()
    {
        // Arrange
        SeedRecipeWithIngredients("Recipe A", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        SeedRecipeWithIngredients("Recipe B", [
            BuildUnresolvedIngredient(CanonicalIngredientId2, "1 jar marinara sauce")
        ]);

        // Act
        var result = await _sut.GetUnresolvedRecipesAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUnresolvedRecipesAsync_ReturnsRecipesOrderedAlphabeticallyByTitle()
    {
        // Arrange
        SeedRecipeWithIngredients("Zesty Chili", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can beans")
        ]);
        SeedRecipeWithIngredients("Arrabiata Sauce", [
            BuildUnresolvedIngredient(CanonicalIngredientId2, "1 can tomatoes")
        ]);

        // Act
        var result = await _sut.GetUnresolvedRecipesAsync();

        // Assert
        result.Select(r => r.Title).Should().BeInAscendingOrder();
    }

    // ── ResolveAsync — validation errors ─────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ZeroQuantity_ReturnsValidationError()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 0m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsValidationError.Should().BeTrue();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResolveAsync_NegativeQuantity_ReturnsValidationError()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = -5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsValidationError.Should().BeTrue();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResolveAsync_UnknownUnitOfMeasureId_ReturnsValidationError()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var nonExistentUnitOfMeasureId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = nonExistentUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsValidationError.Should().BeTrue();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ── ResolveAsync — not found results ─────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_RecipeDoesNotExist_ReturnsRecipeNotFound()
    {
        // Arrange
        var nonExistentRecipeId = Guid.NewGuid();
        var nonExistentIngredientId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(nonExistentRecipeId, nonExistentIngredientId, request);

        // Assert
        result.IsRecipeNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_IngredientDoesNotBelongToRecipe_ReturnsIngredientNotFound()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);

        // Ingredient from a completely different recipe
        var otherRecipe = SeedRecipeWithIngredients("Pasta", [
            BuildUnresolvedIngredient(CanonicalIngredientId2, "1 jar marinara sauce")
        ]);
        var foreignIngredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == otherRecipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 24m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, foreignIngredientId, request);

        // Assert
        result.IsIngredientNotFound.Should().BeTrue();
    }

    // ── ResolveAsync — success path ───────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedIngredient.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ValidRequest_SetsIsContainerResolvedToTrue()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.ResolvedIngredient!.IsContainerResolved.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_ValidRequest_StoresDeclaredQuantityOnIngredient()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        const decimal declaredQuantity = 14.5m;
        var request = new ResolveContainerRequest { Quantity = declaredQuantity, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.ResolvedIngredient!.Quantity.Should().Be(declaredQuantity);
    }

    [Fact]
    public async Task ResolveAsync_ValidRequest_StoresDeclaredUnitOfMeasureIdOnIngredient()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.ResolvedIngredient!.UnitOfMeasureId.Should().Be(OzUnitOfMeasureId);
    }

    [Fact]
    public async Task ResolveAsync_ValidRequest_PreservesOriginalNotesUnchanged()
    {
        // Arrange — Notes must survive resolution unchanged so the UI can show the original text.
        const string originalNotes = "1 can chopped tomatoes";
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, originalNotes)
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.ResolvedIngredient!.Notes.Should().Be(originalNotes);
    }

    [Fact]
    public async Task ResolveAsync_ValidRequest_PersistsChangesToDatabase()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert — reload from the context to confirm the write went through
        var persisted = await _dbContext.RecipeIngredients
            .AsNoTracking()
            .FirstAsync(ri => ri.Id == ingredientId);

        persisted.IsContainerResolved.Should().BeTrue();
        persisted.Quantity.Should().Be(14.5m);
        persisted.UnitOfMeasureId.Should().Be(OzUnitOfMeasureId);
    }

    [Fact]
    public async Task ResolveAsync_JarMarinaraSauce_PreservesNotes()
    {
        // Arrange — verifies the MEP-003 acceptance criterion scenario exactly:
        // "1 jar marinara sauce" resolved to 24 oz must still show that string in Notes.
        const string originalNotes = "1 jar marinara sauce";
        var recipe = SeedRecipeWithIngredients("Pasta Night", [
            BuildUnresolvedIngredient(CanonicalIngredientId2, originalNotes)
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 24m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedIngredient!.Notes.Should().Be(originalNotes);
        result.ResolvedIngredient.Quantity.Should().Be(24m);
        result.ResolvedIngredient.UnitOfMeasureId.Should().Be(OzUnitOfMeasureId);
        result.ResolvedIngredient.IsContainerResolved.Should().BeTrue();
    }

    // ── IsSuccess is mutually exclusive with error outcomes ───────────────────

    [Fact]
    public async Task ResolveAsync_ZeroQuantity_IsSuccessIsFalse()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 0m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ResolvedIngredient.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_RecipeNotFound_IsSuccessIsFalse()
    {
        // Arrange
        var request = new ResolveContainerRequest { Quantity = 14.5m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        var result = await _sut.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ResolvedIngredient.Should().BeNull();
    }

    // ── Fully resolved recipe enters matching pool ────────────────────────────

    [Fact]
    public async Task ResolveAsync_LastUnresolvedIngredient_RecipeIsFullyResolvedAfterSave()
    {
        // Arrange — recipe has exactly one unresolved ingredient; after resolution the recipe
        // is fully resolved and must not appear in the unresolved queue any more.
        var recipe = SeedRecipeWithIngredients("Simple Chili", [
            BuildResolvedIngredient(CanonicalIngredientId1),
            BuildUnresolvedIngredient(CanonicalIngredientId3, "1 can kidney beans")
        ]);
        var unresolvedId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id && !ri.IsContainerResolved).Id;

        var request = new ResolveContainerRequest { Quantity = 15m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        await _sut.ResolveAsync(recipe.Id, unresolvedId, request);
        var unresolvedRecipes = await _sut.GetUnresolvedRecipesAsync();

        // Assert — recipe must no longer appear in the unresolved list
        unresolvedRecipes.Should().NotContain(r => r.Id == recipe.Id);
    }

    [Fact]
    public async Task ResolveAsync_LastUnresolvedIngredient_GetUnresolvedIngredientsReturnsEmptyList()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Simple Chili", [
            BuildUnresolvedIngredient(CanonicalIngredientId3, "1 can kidney beans")
        ]);
        var unresolvedId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 15m, UnitOfMeasureId = OzUnitOfMeasureId };

        // Act
        await _sut.ResolveAsync(recipe.Id, unresolvedId, request);
        var remaining = await _sut.GetUnresolvedIngredientsAsync(recipe.Id);

        // Assert
        remaining.Should().NotBeNull();
        remaining!.Should().BeEmpty();
    }

    // ── MEP-026 phase 5: GetUnresolvedGroupsAsync ─────────────────────────────
    //
    // Scenario: Groups unresolved ingredients by (canonical, notes)
    //   Given multiple recipes with "1 can diced tomatoes" entries
    //   When GetUnresolvedGroupsAsync is called
    //   Then one group is returned with OccurrenceCount equal to the number of such ingredients
    //
    // Scenario: Different canonical ingredients produce distinct groups
    //   Given unresolved ingredients for diced tomatoes and for marinara sauce
    //   When GetUnresolvedGroupsAsync is called
    //   Then two separate groups are returned
    //
    // Scenario: Resolved ingredients are excluded from groups
    //   Given a mix of resolved and unresolved ingredients sharing a canonical
    //   When GetUnresolvedGroupsAsync is called
    //   Then the resolved ones are not counted
    //
    // Scenario: Groups are ordered by occurrence count descending
    //   Given group A with 5 occurrences and group B with 2 occurrences
    //   When GetUnresolvedGroupsAsync is called
    //   Then group A precedes group B in the result
    //
    // Scenario: Empty state returns an empty list, not null
    //   Given no unresolved ingredients exist
    //   When GetUnresolvedGroupsAsync is called
    //   Then the result is empty (but non-null)

    [Fact]
    public async Task GetUnresolvedGroupsAsync_GroupsByCanonicalAndNotes_ReturnsAccurateCount()
    {
        // Arrange — three recipes all referencing "1 can diced tomatoes"
        SeedRecipeWithIngredients("A", [BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")]);
        SeedRecipeWithIngredients("B", [BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")]);
        SeedRecipeWithIngredients("C", [BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")]);

        // Act
        var groups = await _sut.GetUnresolvedGroupsAsync();

        // Assert
        groups.Should().HaveCount(1);
        groups[0].CanonicalIngredientId.Should().Be(CanonicalIngredientId1);
        groups[0].Notes.Should().Be("1 can diced tomatoes");
        groups[0].OccurrenceCount.Should().Be(3);
    }

    [Fact]
    public async Task GetUnresolvedGroupsAsync_DifferentCanonicals_ProducesDistinctGroups()
    {
        SeedRecipeWithIngredients("A", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes"),
            BuildUnresolvedIngredient(CanonicalIngredientId2, "1 jar marinara sauce")
        ]);

        var groups = await _sut.GetUnresolvedGroupsAsync();

        groups.Should().HaveCount(2);
        groups.Select(g => g.CanonicalIngredientId)
            .Should().BeEquivalentTo(new[] { CanonicalIngredientId1, CanonicalIngredientId2 });
    }

    [Fact]
    public async Task GetUnresolvedGroupsAsync_ResolvedIngredientsExcluded()
    {
        SeedRecipeWithIngredients("A", [
            BuildResolvedIngredient(CanonicalIngredientId1), // already resolved -- excluded
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);

        var groups = await _sut.GetUnresolvedGroupsAsync();

        groups.Should().HaveCount(1);
        groups[0].OccurrenceCount.Should().Be(1);
    }

    [Fact]
    public async Task GetUnresolvedGroupsAsync_OrderedByOccurrenceCountDescending()
    {
        // group A: 1 can beans × 5 (marinara canonical reused here for variety)
        // group B: 1 can marinara × 2
        for (var i = 0; i < 5; i++)
        {
            SeedRecipeWithIngredients($"A{i}", [
                BuildUnresolvedIngredient(CanonicalIngredientId3, "1 can kidney beans")
            ]);
        }
        for (var i = 0; i < 2; i++)
        {
            SeedRecipeWithIngredients($"B{i}", [
                BuildUnresolvedIngredient(CanonicalIngredientId2, "1 jar marinara sauce")
            ]);
        }

        var groups = await _sut.GetUnresolvedGroupsAsync();

        groups.Should().HaveCount(2);
        groups[0].OccurrenceCount.Should().Be(5);
        groups[1].OccurrenceCount.Should().Be(2);
    }

    [Fact]
    public async Task GetUnresolvedGroupsAsync_EmptyState_ReturnsEmptyList()
    {
        var groups = await _sut.GetUnresolvedGroupsAsync();

        groups.Should().BeEmpty();
    }

    // ── MEP-026 phase 5: BulkResolveAsync ─────────────────────────────────────
    //
    // Scenario: Updates all matching unresolved rows in one call
    //   Given 3 recipes each with "1 can diced tomatoes" for the same canonical
    //   When BulkResolveAsync is called with that key and a valid declaration
    //   Then all 3 rows are updated (IsContainerResolved = true, Quantity / UnitOfMeasureId set)
    //   And AffectedCount is 3
    //
    // Scenario: Does not touch rows in a different group
    //   Given one "1 can diced tomatoes" row and one "1 jar marinara" row
    //   When BulkResolveAsync is called with the diced-tomatoes key
    //   Then only the diced-tomatoes row is updated
    //
    // Scenario: Already-resolved rows are not re-written
    //   Given one unresolved and one resolved row sharing the same canonical + notes
    //   When BulkResolveAsync is called
    //   Then only the unresolved row is updated (AffectedCount = 1)
    //
    // Scenario: Non-positive quantity is rejected
    //   When BulkResolveAsync is called with Quantity = 0
    //   Then IsValidationError is true
    //
    // Scenario: Unknown unit of measure is rejected
    //   When BulkResolveAsync is called with a UnitOfMeasureId that does not exist
    //   Then IsValidationError is true
    //
    // Scenario: Empty notes key is rejected
    //   When BulkResolveAsync is called with an empty notes string
    //   Then IsValidationError is true
    //
    // Scenario: Notes match is case-insensitive
    //   Given a row with Notes = "1 Can Diced Tomatoes"
    //   When BulkResolveAsync is called with notes "1 can diced tomatoes"
    //   Then the row is updated

    [Fact]
    public async Task BulkResolveAsync_UpdatesAllMatchingRowsAcrossRecipes()
    {
        SeedRecipeWithIngredients("A", [BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")]);
        SeedRecipeWithIngredients("B", [BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")]);
        SeedRecipeWithIngredients("C", [BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")]);

        var result = await _sut.BulkResolveAsync(CanonicalIngredientId1, "1 can diced tomatoes", 14.5m, OzUnitOfMeasureId);

        result.IsValidationError.Should().BeFalse();
        result.AffectedCount.Should().Be(3);

        var rows = await _dbContext.RecipeIngredients
            .Where(ri => ri.CanonicalIngredientId == CanonicalIngredientId1)
            .ToListAsync();
        rows.Should().OnlyContain(ri => ri.IsContainerResolved
                                        && ri.Quantity == 14.5m
                                        && ri.UnitOfMeasureId == OzUnitOfMeasureId);
        rows.Should().OnlyContain(ri => ri.Notes == "1 can diced tomatoes"); // preserved
    }

    [Fact]
    public async Task BulkResolveAsync_DoesNotTouchOtherGroups()
    {
        SeedRecipeWithIngredients("A", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes"),
            BuildUnresolvedIngredient(CanonicalIngredientId2, "1 jar marinara sauce")
        ]);

        await _sut.BulkResolveAsync(CanonicalIngredientId1, "1 can diced tomatoes", 14.5m, OzUnitOfMeasureId);

        var marinara = await _dbContext.RecipeIngredients
            .FirstAsync(ri => ri.CanonicalIngredientId == CanonicalIngredientId2);
        marinara.IsContainerResolved.Should().BeFalse();
    }

    [Fact]
    public async Task BulkResolveAsync_AlreadyResolvedRowsAreSkipped()
    {
        SeedRecipeWithIngredients("A", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes"),
            BuildResolvedIngredient(CanonicalIngredientId1)
        ]);

        var result = await _sut.BulkResolveAsync(CanonicalIngredientId1, "1 can diced tomatoes", 14.5m, OzUnitOfMeasureId);

        result.AffectedCount.Should().Be(1);
    }

    [Fact]
    public async Task BulkResolveAsync_NonPositiveQuantity_IsRejected()
    {
        var result = await _sut.BulkResolveAsync(CanonicalIngredientId1, "1 can diced tomatoes", 0m, OzUnitOfMeasureId);

        result.IsValidationError.Should().BeTrue();
        result.AffectedCount.Should().Be(0);
    }

    [Fact]
    public async Task BulkResolveAsync_UnknownUnitOfMeasure_IsRejected()
    {
        var result = await _sut.BulkResolveAsync(CanonicalIngredientId1, "1 can diced tomatoes", 14.5m, Guid.NewGuid());

        result.IsValidationError.Should().BeTrue();
    }

    [Fact]
    public async Task BulkResolveAsync_EmptyNotes_IsRejected()
    {
        var result = await _sut.BulkResolveAsync(CanonicalIngredientId1, "   ", 14.5m, OzUnitOfMeasureId);

        result.IsValidationError.Should().BeTrue();
    }

    [Fact]
    public async Task BulkResolveAsync_NotesMatchIsCaseInsensitive()
    {
        SeedRecipeWithIngredients("A", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 Can Diced Tomatoes")
        ]);

        var result = await _sut.BulkResolveAsync(CanonicalIngredientId1, "1 can diced tomatoes", 14.5m, OzUnitOfMeasureId);

        result.AffectedCount.Should().Be(1);
    }
}
