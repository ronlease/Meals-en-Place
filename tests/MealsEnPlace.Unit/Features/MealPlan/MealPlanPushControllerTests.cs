// Feature: Meal Plan Push Controller (MEP-029)
//
// Scenario: Success returns 200 with the service's result payload
// Scenario: "not configured" from the target maps to 400 Bad Request
// Scenario: "was not found" from the target maps to 404 Not Found

using FluentAssertions;
using MealsEnPlace.Api.Features.MealPlan;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.MealPlan;

public sealed class MealPlanPushControllerTests
{
    private readonly MealPlanPushController _sut;
    private readonly Mock<IMealPlanPushTarget> _targetMock = new(MockBehavior.Strict);

    public MealPlanPushControllerTests()
    {
        _sut = new MealPlanPushController(_targetMock.Object);
    }

    [Fact]
    public async Task PushToTodoist_Success_Returns200WithResult()
    {
        var planId = Guid.NewGuid();
        var result = new MealPlanPushResult { Created = 14 };
        _targetMock
            .Setup(t => t.PushAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var action = await _sut.PushToTodoist(planId);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(result);
    }

    [Fact]
    public async Task PushToTodoist_NotConfigured_Returns400()
    {
        _targetMock
            .Setup(t => t.PushAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Todoist integration is not configured."));

        var action = await _sut.PushToTodoist(Guid.NewGuid());

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PushToTodoist_PlanNotFound_Returns404()
    {
        _targetMock
            .Setup(t => t.PushAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Meal plan 'abc' was not found."));

        var action = await _sut.PushToTodoist(Guid.NewGuid());

        action.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
