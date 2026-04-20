// Feature: Shopping List Push Controller (MEP-028)
//
// Scenario: Meal-plan push returns 200 with the service's result payload
// Scenario: Standalone push returns 200 with the service's result payload
// Scenario: "not configured" propagates from the target as a 400 Bad Request
// Scenario: Non-configuration exceptions are not swallowed by the controller

using FluentAssertions;
using MealsEnPlace.Api.Features.ShoppingList;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.ShoppingList;

public sealed class ShoppingListPushControllerTests
{
    private readonly ShoppingListPushController _sut;
    private readonly Mock<IShoppingListPushTarget> _targetMock = new(MockBehavior.Strict);

    public ShoppingListPushControllerTests()
    {
        _sut = new ShoppingListPushController(_targetMock.Object);
    }

    [Fact]
    public async Task PushMealPlanShoppingList_Success_Returns200WithResult()
    {
        var mealPlanId = Guid.NewGuid();
        var result = new ShoppingListPushResult { Created = 2, Unchanged = 1 };
        _targetMock
            .Setup(t => t.PushAsync(mealPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var action = await _sut.PushMealPlanShoppingList(mealPlanId);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(result);
    }

    [Fact]
    public async Task PushStandaloneShoppingList_Success_Returns200WithResult()
    {
        var result = new ShoppingListPushResult { Created = 1 };
        _targetMock
            .Setup(t => t.PushAsync((Guid?)null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var action = await _sut.PushStandaloneShoppingList();

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(result);
    }

    [Fact]
    public async Task PushMealPlanShoppingList_NotConfigured_Returns400()
    {
        _targetMock
            .Setup(t => t.PushAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Todoist integration is not configured."));

        var action = await _sut.PushMealPlanShoppingList(Guid.NewGuid());

        action.Result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task PushMealPlanShoppingList_UnrelatedException_IsNotSwallowed()
    {
        _targetMock
            .Setup(t => t.PushAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("some other failure"));

        var act = async () => await _sut.PushMealPlanShoppingList(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("some other failure");
    }
}
