// Feature: Seasonal Produce Controller
//
// Scenario: Get all windows — returns 200 with all seasonality windows
//   Given seasonality windows exist
//   When GET /api/v1/seasonal-produce/all is called
//   Then the response is 200 OK with all windows
//
// Scenario: Get in-season produce — returns 200 with current season
//   Given produce is in season
//   When GET /api/v1/seasonal-produce is called
//   Then the response is 200 OK with in-season produce

using FluentAssertions;
using MealsEnPlace.Api.Features.SeasonalProduce;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.SeasonalProduce;

public class SeasonalProduceControllerTests
{
    private readonly SeasonalProduceController _controller;
    private readonly Mock<ISeasonalProduceService> _mockService = new();

    public SeasonalProduceControllerTests()
    {
        _controller = new SeasonalProduceController(_mockService.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsAllWindows()
    {
        var windows = new List<SeasonalProduceResponse>
        {
            new() { IngredientId = Guid.NewGuid(), Name = "Tomato", PeakSeasonEnd = Month.September, PeakSeasonStart = Month.June, UsdaZone = "7a" }
        };
        _mockService.Setup(s => s.GetAllWindowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(windows);

        var result = await _controller.GetAll();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(windows);
    }

    [Fact]
    public async Task GetInSeason_ReturnsInSeasonProduce()
    {
        var produce = new List<SeasonalProduceResponse>
        {
            new() { IngredientId = Guid.NewGuid(), Name = "Strawberry", PeakSeasonEnd = Month.June, PeakSeasonStart = Month.April, UsdaZone = "7a" }
        };
        _mockService.Setup(s => s.GetInSeasonAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(produce);

        var result = await _controller.GetInSeason();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(produce);
    }
}
