// Feature: Meal Plan Push Controller (MEP-029 / MEP-036)
//
// Scenario: Success returns 200 with the service's result payload
// Scenario: "not configured" from the target maps to 400 Bad Request
// Scenario: "was not found" from the target maps to 404 Not Found
// Scenario: Push with a project override forwards it to the target
// Scenario: Push with no request body passes null override to the target

using FluentAssertions;
using MealsEnPlace.Api.Features.MealPlan;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
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
            .Setup(t => t.PushAsync(planId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var action = await _sut.PushToTodoist(planId);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(result);
    }

    [Fact]
    public async Task PushToTodoist_WithProjectOverride_ForwardsOverrideToTarget()
    {
        var planId = Guid.NewGuid();
        var result = new MealPlanPushResult { Created = 7 };
        _targetMock
            .Setup(t => t.PushAsync(planId, "2331547980", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var action = await _sut.PushToTodoist(planId, new TodoistPushRequest { ProjectId = "2331547980" });

        action.Result.Should().BeOfType<OkObjectResult>();
        _targetMock.Verify(t => t.PushAsync(planId, "2331547980", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushToTodoist_WithNullRequestBody_PassesNullOverride()
    {
        var planId = Guid.NewGuid();
        var result = new MealPlanPushResult { Created = 7 };
        _targetMock
            .Setup(t => t.PushAsync(planId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var action = await _sut.PushToTodoist(planId, request: null);

        action.Result.Should().BeOfType<OkObjectResult>();
        _targetMock.Verify(t => t.PushAsync(planId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PushToTodoist_NotConfigured_Returns400()
    {
        _targetMock
            .Setup(t => t.PushAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Todoist integration is not configured."));

        var action = await _sut.PushToTodoist(Guid.NewGuid());

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PushToTodoist_PlanNotFound_Returns404()
    {
        _targetMock
            .Setup(t => t.PushAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Meal plan 'abc' was not found."));

        var action = await _sut.PushToTodoist(Guid.NewGuid());

        action.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
