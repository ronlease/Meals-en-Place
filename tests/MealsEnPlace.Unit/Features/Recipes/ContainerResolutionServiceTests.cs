// Feature: Container Reference Resolution — Recipe Side
//
// Scenario: Resolve an unresolved ingredient — sets IsContainerResolved = true
//   Given a recipe with one unresolved RecipeIngredient
//   And a valid ResolveContainerRequest with positive quantity and a known UomId
//   When ResolveAsync is called
//   Then the result IsSuccess is true
//   And the returned ingredient has IsContainerResolved = true
//
// Scenario: Resolve an unresolved ingredient — stores correct quantity and UOM
//   Given a recipe with one unresolved RecipeIngredient
//   And a ResolveContainerRequest with Quantity 14.5 and a known oz UomId
//   When ResolveAsync is called
//   Then the resolved ingredient Quantity equals 14.5
//   And the resolved ingredient UomId equals the declared oz UomId
//
// Scenario: Resolve an unresolved ingredient — Notes field is preserved unchanged
//   Given a RecipeIngredient with Notes "1 can chopped tomatoes" and IsContainerResolved = false
//   When ResolveAsync is called with a valid declaration
//   Then the resolved ingredient Notes still equals "1 can chopped tomatoes"
//
// Scenario: Resolve with invalid UOM id — returns validation error
//   Given a ResolveContainerRequest whose UomId does not exist in the database
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
//   Given a valid recipe and a valid UOM
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
    private static readonly Guid OzUomId = Guid.NewGuid();
    private static readonly Guid GramsUomId = Guid.NewGuid();

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
                Id = GramsUomId,
                Name = "Gram",
                UomType = UomType.Weight
            },
            new UnitOfMeasure
            {
                Abbreviation = "oz",
                ConversionFactor = 28.3495m,
                Id = OzUomId,
                Name = "Ounce",
                UomType = UomType.Weight
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
            DefaultUomId = GramsUomId,
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
            UomId = null
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
            UomId = GramsUomId
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

        var request = new ResolveContainerRequest { Quantity = 0m, UomId = OzUomId };

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

        var request = new ResolveContainerRequest { Quantity = -5m, UomId = OzUomId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsValidationError.Should().BeTrue();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResolveAsync_UnknownUomId_ReturnsValidationError()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var nonExistentUomId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = nonExistentUomId };

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
        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = OzUomId };

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

        var request = new ResolveContainerRequest { Quantity = 24m, UomId = OzUomId };

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

        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = OzUomId };

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

        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = OzUomId };

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
        var request = new ResolveContainerRequest { Quantity = declaredQuantity, UomId = OzUomId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.ResolvedIngredient!.Quantity.Should().Be(declaredQuantity);
    }

    [Fact]
    public async Task ResolveAsync_ValidRequest_StoresDeclaredUomIdOnIngredient()
    {
        // Arrange
        var recipe = SeedRecipeWithIngredients("Tomato Soup", [
            BuildUnresolvedIngredient(CanonicalIngredientId1, "1 can diced tomatoes")
        ]);
        var ingredientId = _dbContext.RecipeIngredients
            .First(ri => ri.RecipeId == recipe.Id).Id;

        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = OzUomId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.ResolvedIngredient!.UomId.Should().Be(OzUomId);
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

        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = OzUomId };

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

        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = OzUomId };

        // Act
        await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert — reload from the context to confirm the write went through
        var persisted = await _dbContext.RecipeIngredients
            .AsNoTracking()
            .FirstAsync(ri => ri.Id == ingredientId);

        persisted.IsContainerResolved.Should().BeTrue();
        persisted.Quantity.Should().Be(14.5m);
        persisted.UomId.Should().Be(OzUomId);
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

        var request = new ResolveContainerRequest { Quantity = 24m, UomId = OzUomId };

        // Act
        var result = await _sut.ResolveAsync(recipe.Id, ingredientId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ResolvedIngredient!.Notes.Should().Be(originalNotes);
        result.ResolvedIngredient.Quantity.Should().Be(24m);
        result.ResolvedIngredient.UomId.Should().Be(OzUomId);
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

        var request = new ResolveContainerRequest { Quantity = 0m, UomId = OzUomId };

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
        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = OzUomId };

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

        var request = new ResolveContainerRequest { Quantity = 15m, UomId = OzUomId };

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

        var request = new ResolveContainerRequest { Quantity = 15m, UomId = OzUomId };

        // Act
        await _sut.ResolveAsync(recipe.Id, unresolvedId, request);
        var remaining = await _sut.GetUnresolvedIngredientsAsync(recipe.Id);

        // Assert
        remaining.Should().NotBeNull();
        remaining!.Should().BeEmpty();
    }
}
