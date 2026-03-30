// Feature: Container Resolution Controller
//
// Scenario: Get unresolved recipes — returns 200 with list
//   Given recipes with unresolved container references exist
//   When GET /api/v1/recipes/unresolved is called
//   Then the response is 200 OK with the unresolved recipe list
//
// Scenario: Get unresolved ingredients — returns 200 when recipe exists
//   Given a recipe with unresolved ingredients exists
//   When GET /api/v1/recipes/{recipeId}/unresolved-ingredients is called
//   Then the response is 200 OK with the unresolved ingredient list
//
// Scenario: Get unresolved ingredients — returns 404 when recipe not found
//   Given no recipe exists for the given ID
//   When GET /api/v1/recipes/{recipeId}/unresolved-ingredients is called
//   Then the response is 404 Not Found
//
// Scenario: Resolve ingredient — returns 200 on success
//   Given a valid resolve request
//   When PUT /api/v1/recipes/{recipeId}/ingredients/{ingredientId}/resolve is called
//   Then the response is 200 OK with the resolved ingredient
//
// Scenario: Resolve ingredient — returns 404 when recipe not found
//   Given the service returns RecipeNotFound
//   When PUT resolve is called
//   Then the response is 404 Not Found
//
// Scenario: Resolve ingredient — returns 404 when ingredient not found
//   Given the service returns IngredientNotFound
//   When PUT resolve is called
//   Then the response is 404 Not Found
//
// Scenario: Resolve ingredient — returns 400 on validation error
//   Given the service returns a ValidationError
//   When PUT resolve is called
//   Then the response is 400 Bad Request

using FluentAssertions;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MealsEnPlace.Unit.Features.Recipes;

public class ContainerResolutionControllerTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly ContainerResolutionController _sut;
    private readonly Mock<IContainerResolutionService> _serviceMock = new(MockBehavior.Strict);
    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly UomDisplayConverter _displayConverter;

    public ContainerResolutionControllerTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        _displayConverter = new UomDisplayConverter(_dbContext);
        _sut = new ContainerResolutionController(_serviceMock.Object, _displayConverter);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── GetUnresolvedRecipes ──────────────────────────────────────────────────

    [Fact]
    public async Task GetUnresolvedRecipes_RecipesExist_Returns200WithList()
    {
        // Arrange
        var recipes = new List<Recipe>
        {
            BuildRecipeWithUnresolvedIngredient()
        };
        _serviceMock
            .Setup(s => s.GetUnresolvedRecipesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipes);

        // Act
        var result = await _sut.GetUnresolvedRecipes(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetUnresolvedRecipes_EmptyList_Returns200WithEmptyList()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetUnresolvedRecipesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetUnresolvedRecipes(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var value = ok.Value.Should().BeAssignableTo<IReadOnlyList<UnresolvedRecipeResponse>>().Subject;
        value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnresolvedRecipes_CallsServiceOnce()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetUnresolvedRecipesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.GetUnresolvedRecipes(CancellationToken.None);

        // Assert
        _serviceMock.Verify(s => s.GetUnresolvedRecipesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetUnresolvedIngredients ──────────────────────────────────────────────

    [Fact]
    public async Task GetUnresolvedIngredients_RecipeExists_Returns200()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        IReadOnlyList<RecipeIngredient>? ingredients = new List<RecipeIngredient>();
        _serviceMock
            .Setup(s => s.GetUnresolvedIngredientsAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ingredients);

        // Act
        var result = await _sut.GetUnresolvedIngredients(recipeId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetUnresolvedIngredients_RecipeNotFound_Returns404()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GetUnresolvedIngredientsAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RecipeIngredient>?)null);

        // Act
        var result = await _sut.GetUnresolvedIngredients(recipeId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetUnresolvedIngredients_RecipeNotFound_ProblemDetailContainsRecipeId()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GetUnresolvedIngredientsAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RecipeIngredient>?)null);

        // Act
        var result = await _sut.GetUnresolvedIngredients(recipeId, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain(recipeId.ToString());
    }

    // ── ResolveIngredient — success ───────────────────────────────────────────

    [Fact]
    public async Task ResolveIngredient_Success_Returns200()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = Guid.NewGuid() };
        var ingredient = BuildResolvedIngredient(recipeId, ingredientId);
        _serviceMock
            .Setup(s => s.ResolveAsync(recipeId, ingredientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContainerResolutionResult.Success(ingredient));

        // Act
        var result = await _sut.ResolveIngredient(recipeId, ingredientId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ResolveIngredient_Success_ResponseBodyContainsResolvedIngredient()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = Guid.NewGuid() };
        var ingredient = BuildResolvedIngredient(recipeId, ingredientId);
        _serviceMock
            .Setup(s => s.ResolveAsync(recipeId, ingredientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContainerResolutionResult.Success(ingredient));

        // Act
        var result = await _sut.ResolveIngredient(recipeId, ingredientId, request, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ResolvedIngredientResponse>();
    }

    // ── ResolveIngredient — recipe not found ──────────────────────────────────

    [Fact]
    public async Task ResolveIngredient_RecipeNotFound_Returns404()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = Guid.NewGuid() };
        _serviceMock
            .Setup(s => s.ResolveAsync(recipeId, ingredientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContainerResolutionResult.RecipeNotFound());

        // Act
        var result = await _sut.ResolveIngredient(recipeId, ingredientId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    // ── ResolveIngredient — ingredient not found ──────────────────────────────

    [Fact]
    public async Task ResolveIngredient_IngredientNotFound_Returns404()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = 14.5m, UomId = Guid.NewGuid() };
        _serviceMock
            .Setup(s => s.ResolveAsync(recipeId, ingredientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContainerResolutionResult.IngredientNotFound());

        // Act
        var result = await _sut.ResolveIngredient(recipeId, ingredientId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    // ── ResolveIngredient — validation error ──────────────────────────────────

    [Fact]
    public async Task ResolveIngredient_ValidationError_Returns400()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = -1m, UomId = Guid.NewGuid() };
        _serviceMock
            .Setup(s => s.ResolveAsync(recipeId, ingredientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContainerResolutionResult.ValidationError("Quantity must be greater than zero."));

        // Act
        var result = await _sut.ResolveIngredient(recipeId, ingredientId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResolveIngredient_ValidationError_ProblemDetailContainsErrorMessage()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var request = new ResolveContainerRequest { Quantity = -1m, UomId = Guid.NewGuid() };
        _serviceMock
            .Setup(s => s.ResolveAsync(recipeId, ingredientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContainerResolutionResult.ValidationError("Quantity must be greater than zero."));

        // Act
        var result = await _sut.ResolveIngredient(recipeId, ingredientId, request, CancellationToken.None);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("Quantity");
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static Recipe BuildRecipeWithUnresolvedIngredient()
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUomId = Guid.NewGuid(),
            Id = Guid.NewGuid(),
            Name = "Chopped Tomatoes"
        };

        var ri = new RecipeIngredient
        {
            CanonicalIngredient = ingredient,
            CanonicalIngredientId = ingredient.Id,
            Id = Guid.NewGuid(),
            IsContainerResolved = false,
            Notes = "1 can chopped tomatoes",
            Quantity = 0m,
            RecipeId = Guid.NewGuid()
        };

        return new Recipe
        {
            Id = ri.RecipeId,
            Instructions = "Cook it.",
            RecipeIngredients = [ri],
            ServingCount = 4,
            Title = "Pasta Sauce"
        };
    }

    private static RecipeIngredient BuildResolvedIngredient(Guid recipeId, Guid ingredientId)
    {
        var uom = new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1.0m,
            Id = Guid.NewGuid(),
            Name = "gram",
            UomType = UomType.Weight
        };

        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUomId = uom.Id,
            Id = Guid.NewGuid(),
            Name = "Chopped Tomatoes"
        };

        return new RecipeIngredient
        {
            CanonicalIngredient = ingredient,
            CanonicalIngredientId = ingredient.Id,
            Id = ingredientId,
            IsContainerResolved = true,
            Notes = "1 can chopped tomatoes",
            Quantity = 411m,
            RecipeId = recipeId,
            Uom = uom,
            UomId = uom.Id
        };
    }
}
