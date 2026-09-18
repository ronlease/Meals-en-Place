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
// Scenario: GetLocalRecipes returns 200 with paged result from service
//   Given the service returns a non-empty PagedResult of RecipeListItemDto
//   When GetLocalRecipes is called
//   Then the response is 200 OK
//   And the body contains the PagedResult
//
// Scenario: GetLocalRecipes returns 200 with empty page when library is empty
//   Given the service returns an empty PagedResult
//   When GetLocalRecipes is called
//   Then the response is 200 OK with an empty Items list
//
// Scenario: GetLocalRecipes passes page and pageSize to the service
//   Given page=3 and pageSize=50 are supplied
//   When GetLocalRecipes is called
//   Then the service is called with a RecipeSearchQuery where Page=3 and PageSize=50
//
// Scenario: GetLocalRecipes uses default page and pageSize when not supplied
//   Given no pagination query parameters are provided
//   When GetLocalRecipes is called
//   Then the service is called with a RecipeSearchQuery where Page=1 and PageSize=25
//
// Scenario: GetLocalRecipes with q param passes TitleSearch to service
//   Given q="tikka" is supplied
//   When GetLocalRecipes is called
//   Then the service is called with a RecipeSearchQuery where TitleSearch="tikka"
//
// Scenario: GetLocalRecipes with ingredient param passes IngredientSearch to service
//   Given ingredient="broccoli" is supplied
//   When GetLocalRecipes is called
//   Then the service is called with a RecipeSearchQuery where IngredientSearch="broccoli"
//
// Scenario: GetLocalRecipes with a single dietaryTag passes it to service
//   Given dietaryTag=Vegetarian is supplied
//   When GetLocalRecipes is called
//   Then the service is called with a RecipeSearchQuery where DietaryTags=[Vegetarian]
//
// Scenario: GetLocalRecipes with multiple dietaryTag values passes all tags to service
//   Given dietaryTag=Vegan and dietaryTag=GlutenFree are supplied
//   When GetLocalRecipes is called
//   Then the service is called with a RecipeSearchQuery where DietaryTags contains both Vegan and GlutenFree
//
// Scenario: GetLocalRecipes with no filters passes null search terms and empty tag list to service
//   Given no search or filter parameters are supplied
//   When GetLocalRecipes is called
//   Then the service is called with TitleSearch=null, IngredientSearch=null, and an empty DietaryTags list
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

using FluentAssertions;
using MealsEnPlace.Api.Common;
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

    // ── GetLocalRecipes ────────────────────────────────────────────────────

    [Fact]
    public async Task GetLocalRecipes_LibraryHasRecipes_Returns200WithPagedResult()
    {
        // Arrange
        var items = new List<RecipeListItemDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Pasta" },
            new() { Id = Guid.NewGuid(), Title = "Soup" }
        };
        var pagedResult = new PagedResult<RecipeListItemDto>
        {
            Items = items,
            Page = 1,
            PageSize = 25,
            TotalCount = 2
        };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(It.IsAny<RecipeSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(cancellationToken: CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().Be(pagedResult);
    }

    [Fact]
    public async Task GetLocalRecipes_EmptyLibrary_Returns200WithEmptyItems()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto>
        {
            Items = [],
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(It.IsAny<RecipeSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(cancellationToken: CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.As<PagedResult<RecipeListItemDto>>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocalRecipes_WithExplicitPageAndPageSize_PassesValuesToService()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto> { Items = [], Page = 3, PageSize = 50, TotalCount = 0 };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.Page == 3 && q.PageSize == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(page: 3, pageSize: 50, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(
            s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.Page == 3 && q.PageSize == 50),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLocalRecipes_NoParametersSupplied_UsesDefaultPageAndPageSize()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.Page == 1 && q.PageSize == 25),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(
            s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.Page == 1 && q.PageSize == 25),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetLocalRecipes — search and filter parameter forwarding ──────────────

    [Fact]
    public async Task GetLocalRecipes_WithQParam_PassesTitleSearchToService()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.TitleSearch == "tikka"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(q: "tikka", cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(
            s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.TitleSearch == "tikka"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLocalRecipes_WithIngredientParam_PassesIngredientSearchToService()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.IngredientSearch == "broccoli"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(ingredient: "broccoli", cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(
            s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q => q.IngredientSearch == "broccoli"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLocalRecipes_WithSingleDietaryTag_PassesSingleTagToService()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q =>
                    q.DietaryTags.Count == 1 && q.DietaryTags.Contains(DietaryTag.Vegetarian)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(
            dietaryTag: [DietaryTag.Vegetarian],
            cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(
            s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q =>
                    q.DietaryTags.Count == 1 && q.DietaryTags.Contains(DietaryTag.Vegetarian)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLocalRecipes_WithMultipleDietaryTags_PassesAllTagsToService()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q =>
                    q.DietaryTags.Count == 2 &&
                    q.DietaryTags.Contains(DietaryTag.Vegan) &&
                    q.DietaryTags.Contains(DietaryTag.GlutenFree)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(
            dietaryTag: [DietaryTag.Vegan, DietaryTag.GlutenFree],
            cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(
            s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q =>
                    q.DietaryTags.Count == 2 &&
                    q.DietaryTags.Contains(DietaryTag.Vegan) &&
                    q.DietaryTags.Contains(DietaryTag.GlutenFree)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLocalRecipes_NoFilters_PassesNullSearchTermsAndEmptyTagListToService()
    {
        // Arrange
        var pagedResult = new PagedResult<RecipeListItemDto> { Items = [], Page = 1, PageSize = 25, TotalCount = 0 };
        _serviceMock
            .Setup(s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q =>
                    q.TitleSearch == null &&
                    q.IngredientSearch == null &&
                    q.DietaryTags.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetLocalRecipes(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _serviceMock.Verify(
            s => s.GetPagedLocalRecipesAsync(
                It.Is<RecipeSearchQuery>(q =>
                    q.TitleSearch == null &&
                    q.IngredientSearch == null &&
                    q.DietaryTags.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
