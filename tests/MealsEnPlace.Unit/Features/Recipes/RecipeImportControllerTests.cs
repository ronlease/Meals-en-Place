// Feature: Recipe Import Controller
//
// Scenario: Create with valid title and ingredients returns 201 with created recipe
//   Given a CreateRecipeRequest with a non-empty Title and at least one Ingredient
//   When Create is called
//   Then the response is 201 Created
//   And the body contains the RecipeDetailDto returned by the service
//   And the Location header points to GetById
//
// Scenario: Create with empty title returns 400 Bad Request
//   Given a CreateRecipeRequest where Title is empty
//   When Create is called
//   Then the response is 400 Bad Request
//   And the service is never called
//
// Scenario: Create with whitespace-only title returns 400 Bad Request
//   Given a CreateRecipeRequest where Title is whitespace
//   When Create is called
//   Then the response is 400 Bad Request
//
// Scenario: Create with no ingredients returns 400 Bad Request
//   Given a CreateRecipeRequest with a valid Title but an empty Ingredients list
//   When Create is called
//   Then the response is 400 Bad Request
//
// Scenario: GetAllLocalRecipes returns 200 with list from service
//   Given the service returns a non-empty list of RecipeListItemDto
//   When GetAllLocalRecipes is called
//   Then the response is 200 OK
//   And the body contains the list
//
// Scenario: GetAllLocalRecipes returns 200 with empty list when library is empty
//   Given the service returns an empty list
//   When GetAllLocalRecipes is called
//   Then the response is 200 OK with an empty list
//
// Scenario: GetById with known id returns 200 with recipe detail
//   Given the service returns a RecipeDetailDto for a given id
//   When GetById is called with that id
//   Then the response is 200 OK with the recipe detail
//
// Scenario: GetById with unknown id returns 404 Not Found
//   Given the service returns null for a given id
//   When GetById is called with that id
//   Then the response is 404 Not Found
//
// Scenario: ImportById succeeds and returns 201 Created
//   Given the service successfully imports a recipe for a given mealDbId
//   When ImportById is called
//   Then the response is 201 Created with the RecipeImportResultDto
//
// Scenario: ImportById for a recipe already imported returns 409 Conflict
//   Given the service throws InvalidOperationException containing "already been imported"
//   When ImportById is called
//   Then the response is 409 Conflict
//
// Scenario: ImportById for a meal not found on TheMealDB returns 404 Not Found
//   Given the service throws InvalidOperationException containing "No meal found"
//   When ImportById is called
//   Then the response is 404 Not Found
//
// Scenario: Search with valid query returns 200 with results
//   Given the service returns search results for a query
//   When Search is called with a non-empty query
//   Then the response is 200 OK
//
// Scenario: Search with empty query returns 400 Bad Request
//   Given query parameter is empty
//   When Search is called
//   Then the response is 400 Bad Request and the service is never called
//
// Scenario: Search with whitespace query returns 400 Bad Request
//   Given query parameter is whitespace
//   When Search is called
//   Then the response is 400 Bad Request
//
// Scenario: SearchByCategory with valid category returns 200 with results
//   Given the service returns results for a category
//   When SearchByCategory is called with a non-empty category
//   Then the response is 200 OK
//
// Scenario: SearchByCategory with empty category returns 400 Bad Request
//   Given category parameter is empty
//   When SearchByCategory is called
//   Then the response is 400 Bad Request and the service is never called

using FluentAssertions;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.Recipes;

public class RecipeImportControllerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IRecipeImportService> _serviceMock = new(MockBehavior.Strict);
    private readonly RecipeImportController _sut;

    public RecipeImportControllerTests()
    {
        _sut = new RecipeImportController(_serviceMock.Object);
    }

    // ── Create — success path ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201Created()
    {
        // Arrange
        var request = BuildValidCreateRequest();
        var dto = BuildRecipeDetailDto();
        _serviceMock
            .Setup(s => s.CreateRecipeAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Create_ValidRequest_ResponseBodyContainsRecipeDetailDto()
    {
        // Arrange
        var request = BuildValidCreateRequest();
        var dto = BuildRecipeDetailDto();
        _serviceMock
            .Setup(s => s.CreateRecipeAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Create_ValidRequest_LocationPointsToGetById()
    {
        // Arrange
        var request = BuildValidCreateRequest();
        var dto = BuildRecipeDetailDto();
        _serviceMock
            .Setup(s => s.CreateRecipeAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(RecipeImportController.GetById));
        created.RouteValues!["id"].Should().Be(dto.Id);
    }

    // ── Create — empty title ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_EmptyTitle_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateRecipeRequest
        {
            Ingredients = [new CreateRecipeIngredientRequest { CanonicalIngredientId = Guid.NewGuid(), Quantity = 1, UnitOfMeasureId = Guid.NewGuid() }],
            Title = string.Empty
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Create_EmptyTitle_ServiceIsNeverCalled()
    {
        // Arrange
        var request = new CreateRecipeRequest
        {
            Ingredients = [new CreateRecipeIngredientRequest { CanonicalIngredientId = Guid.NewGuid(), Quantity = 1, UnitOfMeasureId = Guid.NewGuid() }],
            Title = string.Empty
        };

        // Act
        await _sut.Create(request, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.CreateRecipeAsync(It.IsAny<CreateRecipeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_WhitespaceTitleOnly_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateRecipeRequest
        {
            Ingredients = [new CreateRecipeIngredientRequest { CanonicalIngredientId = Guid.NewGuid(), Quantity = 1, UnitOfMeasureId = Guid.NewGuid() }],
            Title = "   "
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ── Create — empty ingredients ────────────────────────────────────────────

    [Fact]
    public async Task Create_NoIngredients_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateRecipeRequest
        {
            Ingredients = [],
            Title = "Pasta"
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Create_NoIngredients_ServiceIsNeverCalled()
    {
        // Arrange
        var request = new CreateRecipeRequest
        {
            Ingredients = [],
            Title = "Pasta"
        };

        // Act
        await _sut.Create(request, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.CreateRecipeAsync(It.IsAny<CreateRecipeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── GetAllLocalRecipes ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllLocalRecipes_LibraryHasRecipes_Returns200WithList()
    {
        // Arrange
        var items = new List<RecipeListItemDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Pasta" },
            new() { Id = Guid.NewGuid(), Title = "Soup" }
        };
        _serviceMock
            .Setup(s => s.GetAllLocalRecipesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _sut.GetAllLocalRecipes(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task GetAllLocalRecipes_EmptyLibrary_Returns200WithEmptyList()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetAllLocalRecipesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecipeListItemDto>());

        // Act
        var result = await _sut.GetAllLocalRecipes(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.As<IEnumerable<RecipeListItemDto>>().Should().BeEmpty();
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_KnownId_Returns200WithRecipeDetail()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto = BuildRecipeDetailDto(id);
        _serviceMock
            .Setup(s => s.GetRecipeDetailAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.GetById(id, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404NotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GetRecipeDetailAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecipeDetailDto?)null);

        // Act
        var result = await _sut.GetById(id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetById_UnknownId_ProblemDetailContainsId()
    {
        // Arrange
        var id = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GetRecipeDetailAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecipeDetailDto?)null);

        // Act
        var result = await _sut.GetById(id, CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain(id.ToString());
    }

    // ── ImportById — success ──────────────────────────────────────────────────

    [Fact]
    public async Task ImportById_SuccessfulImport_Returns201Created()
    {
        // Arrange
        var dto = new RecipeImportResultDto { RecipeId = Guid.NewGuid(), Title = "Pasta", TotalIngredients = 3 };
        _serviceMock
            .Setup(s => s.ImportByIdAsync("52772", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.ImportById("52772", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task ImportById_SuccessfulImport_ResponseBodyContainsImportResult()
    {
        // Arrange
        var dto = new RecipeImportResultDto { RecipeId = Guid.NewGuid(), Title = "Pasta", TotalIngredients = 3 };
        _serviceMock
            .Setup(s => s.ImportByIdAsync("52772", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _sut.ImportById("52772", CancellationToken.None);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.Value.Should().Be(dto);
    }

    // ── ImportById — duplicate import (critical edge case #8) ─────────────────

    [Fact]
    public async Task ImportById_RecipeAlreadyImported_Returns409Conflict()
    {
        // Arrange — critical edge case #8: duplicate import must surface a conflict, not a duplicate record
        _serviceMock
            .Setup(s => s.ImportByIdAsync("52772", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Recipe '52772' has already been imported."));

        // Act
        var result = await _sut.ImportById("52772", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>()
            .Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task ImportById_RecipeAlreadyImported_ProblemDetailContainsMealDbId()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.ImportByIdAsync("52772", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Recipe '52772' has already been imported."));

        // Act
        var result = await _sut.ImportById("52772", CancellationToken.None);

        // Assert
        var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().NotBeNullOrWhiteSpace();
    }

    // ── ImportById — meal not found on TheMealDB ──────────────────────────────

    [Fact]
    public async Task ImportById_MealNotFoundOnTheMealDb_Returns404NotFound()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.ImportByIdAsync("99999", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No meal found for ID '99999'."));

        // Act
        var result = await _sut.ImportById("99999", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ImportById_MealNotFoundOnTheMealDb_ProblemDetailTitleIsRecipeNotFound()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.ImportByIdAsync("99999", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No meal found for ID '99999'."));

        // Act
        var result = await _sut.ImportById("99999", CancellationToken.None);

        // Assert
        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Recipe Not Found");
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ValidQuery_Returns200WithResults()
    {
        // Arrange
        var results = new List<RecipeSearchResultDto>
        {
            new() { Id = "52772", Title = "Chicken Tikka Masala" }
        };
        _serviceMock
            .Setup(s => s.SearchAsync("chicken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        var result = await _sut.Search("chicken", CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().Be(results);
    }

    [Fact]
    public async Task Search_EmptyQuery_Returns400BadRequest()
    {
        // Arrange — no service setup; service must not be called

        // Act
        var result = await _sut.Search(string.Empty, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Search_EmptyQuery_ServiceIsNeverCalled()
    {
        // Act
        await _sut.Search(string.Empty, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Search_WhitespaceQuery_Returns400BadRequest()
    {
        // Act
        var result = await _sut.Search("   ", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ── SearchByCategory ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchByCategory_ValidCategory_Returns200WithResults()
    {
        // Arrange
        var results = new List<RecipeSearchResultDto>
        {
            new() { Id = "52772", Title = "Chicken Tikka Masala" }
        };
        _serviceMock
            .Setup(s => s.SearchByCategoryAsync("Chicken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        var result = await _sut.SearchByCategory("Chicken", CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().Be(results);
    }

    [Fact]
    public async Task SearchByCategory_EmptyCategory_Returns400BadRequest()
    {
        // Act
        var result = await _sut.SearchByCategory(string.Empty, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SearchByCategory_EmptyCategory_ServiceIsNeverCalled()
    {
        // Act
        await _sut.SearchByCategory(string.Empty, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.SearchByCategoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchByCategory_WhitespaceCategory_Returns400BadRequest()
    {
        // Act
        var result = await _sut.SearchByCategory("  ", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static CreateRecipeRequest BuildValidCreateRequest() =>
        new()
        {
            CuisineType = "Italian",
            Ingredients =
            [
                new CreateRecipeIngredientRequest
                {
                    CanonicalIngredientId = Guid.NewGuid(),
                    Quantity = 200,
                    UnitOfMeasureId = Guid.NewGuid()
                }
            ],
            Instructions = "Cook it.",
            ServingCount = 4,
            Title = "Pasta"
        };

    private static RecipeDetailDto BuildRecipeDetailDto(Guid? id = null) =>
        new()
        {
            CuisineType = "Italian",
            Id = id ?? Guid.NewGuid(),
            Instructions = "Cook it.",
            IsFullyResolved = true,
            ServingCount = 4,
            Title = "Pasta"
        };
}
