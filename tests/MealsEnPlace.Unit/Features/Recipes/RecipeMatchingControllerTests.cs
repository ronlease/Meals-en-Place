// Feature: Recipe Matching Controller
//
// Scenario: Get matches with no filters — returns 200 with response
//   Given no query parameters are provided
//   When GET /api/v1/recipes/match is called
//   Then the response is 200 OK with a RecipeMatchResponse
//
// Scenario: Get matches with cuisine filter — returns 200
//   Given a cuisine query parameter is provided
//   When GET /api/v1/recipes/match?cuisine=Italian is called
//   Then the response is 200 OK
//
// Scenario: Get matches with dietary tags — returns 200
//   Given a comma-separated dietaryTags parameter is provided
//   When GET /api/v1/recipes/match?dietaryTags=Vegetarian,GlutenFree is called
//   Then the response is 200 OK
//
// Scenario: Get matches with seasonalOnly=true — returns 200
//   Given seasonalOnly is true
//   When GET /api/v1/recipes/match?seasonalOnly=true is called
//   Then the response is 200 OK

using FluentAssertions;
using MealsEnPlace.Api.Features.Recipes;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.Recipes;

public class RecipeMatchingControllerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly RecipeMatchingController _sut;
    private readonly Mock<IRecipeMatchingService> _serviceMock = new(MockBehavior.Strict);

    public RecipeMatchingControllerTests()
    {
        _sut = new RecipeMatchingController(_serviceMock.Object);
    }

    // ── GetMatches — no filters ───────────────────────────────────────────────

    [Fact]
    public async Task GetMatches_NoFilters_Returns200WithResponse()
    {
        // Arrange
        var response = BuildEmptyMatchResponse();
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(It.IsAny<RecipeMatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.GetMatches(null, null, false, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetMatches_NoFilters_CallsServiceOnce()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(It.IsAny<RecipeMatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmptyMatchResponse());

        // Act
        await _sut.GetMatches(null, null, false, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.MatchRecipesAsync(It.IsAny<RecipeMatchRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetMatches — with cuisine filter ─────────────────────────────────────

    [Fact]
    public async Task GetMatches_WithCuisineFilter_Returns200()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(
                It.Is<RecipeMatchRequest>(r => r.Cuisine == "Italian"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmptyMatchResponse());

        // Act
        var result = await _sut.GetMatches("Italian", null, false, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetMatches_WithCuisineFilter_PassesCuisineToService()
    {
        // Arrange
        RecipeMatchRequest? capturedRequest = null;
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(It.IsAny<RecipeMatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecipeMatchRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(BuildEmptyMatchResponse());

        // Act
        await _sut.GetMatches("Italian", null, false, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Cuisine.Should().Be("Italian");
    }

    // ── GetMatches — with dietary tags ────────────────────────────────────────

    [Fact]
    public async Task GetMatches_WithDietaryTags_Returns200()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(It.IsAny<RecipeMatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmptyMatchResponse());

        // Act
        var result = await _sut.GetMatches(null, "Vegetarian,GlutenFree", false, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetMatches_WithDietaryTags_ParsesTagsFromCommaSeparatedString()
    {
        // Arrange
        RecipeMatchRequest? capturedRequest = null;
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(It.IsAny<RecipeMatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecipeMatchRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(BuildEmptyMatchResponse());

        // Act
        await _sut.GetMatches(null, "Vegetarian,GlutenFree", false, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.DietaryTags.Should().NotBeNull();
        capturedRequest.DietaryTags.Should().HaveCount(2);
    }

    // ── GetMatches — with seasonalOnly=true ───────────────────────────────────

    [Fact]
    public async Task GetMatches_SeasonalOnlyTrue_Returns200()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(
                It.Is<RecipeMatchRequest>(r => r.SeasonalOnly),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEmptyMatchResponse());

        // Act
        var result = await _sut.GetMatches(null, null, true, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetMatches_SeasonalOnlyTrue_PassesSeasonalOnlyToService()
    {
        // Arrange
        RecipeMatchRequest? capturedRequest = null;
        _serviceMock
            .Setup(s => s.MatchRecipesAsync(It.IsAny<RecipeMatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RecipeMatchRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(BuildEmptyMatchResponse());

        // Act
        await _sut.GetMatches(null, null, true, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.SeasonalOnly.Should().BeTrue();
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static RecipeMatchResponse BuildEmptyMatchResponse() =>
        new()
        {
            FullMatches = [],
            NearMatches = [],
            PartialMatches = []
        };
}
