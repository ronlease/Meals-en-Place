// Feature: Meal Plan Controller
//
// Scenario: Generate meal plan — returns 200 with plan
//   Given a valid GenerateMealPlanRequest
//   When POST /api/v1/meal-plans/generate is called
//   Then the response is 200 OK with the generated plan
//
// Scenario: Get active meal plan — returns 200 when plan exists
//   Given a meal plan exists
//   When GET /api/v1/meal-plans/active is called
//   Then the response is 200 OK with the plan
//
// Scenario: Get active meal plan — returns 404 when no plans exist
//   Given no meal plans exist
//   When GET /api/v1/meal-plans/active is called
//   Then the response is 404 Not Found
//
// Scenario: Get meal plan by ID — returns 200 when found
//   Given a meal plan exists with a known ID
//   When GET /api/v1/meal-plans/{id} is called
//   Then the response is 200 OK with the plan
//
// Scenario: Get meal plan by ID — returns 404 when not found
//   Given no meal plan exists for the given ID
//   When GET /api/v1/meal-plans/{id} is called
//   Then the response is 404 Not Found
//
// Scenario: List all meal plans — returns 200 with list
//   Given meal plans exist
//   When GET /api/v1/meal-plans is called
//   Then the response is 200 OK with all plans
//
// Scenario: Swap slot — returns 200 when slot found
//   Given a meal plan slot exists
//   When PUT /api/v1/meal-plans/slots/{slotId} is called
//   Then the response is 200 OK with the updated slot
//
// Scenario: Swap slot — returns 404 when slot not found
//   Given no slot exists for the given ID
//   When PUT /api/v1/meal-plans/slots/{slotId} is called
//   Then the response is 404 Not Found

using FluentAssertions;
using MealsEnPlace.Api.Features.MealPlan;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.MealPlan;

public class MealPlanControllerTests
{
    private readonly MealPlanController _controller;
    private readonly Mock<IMealPlanService> _mockService = new();

    public MealPlanControllerTests()
    {
        _controller = new MealPlanController(_mockService.Object);
    }

    [Fact]
    public async Task Generate_ValidRequest_Returns200WithPlan()
    {
        var request = new GenerateMealPlanRequest { SeasonalOnly = false };
        var expected = new MealPlanResponse { CreatedAt = DateTime.UtcNow, Id = Guid.NewGuid(), Name = "Week Plan", Slots = [], WeekStartDate = DateOnly.FromDateTime(DateTime.Today) };
        _mockService.Setup(s => s.GenerateMealPlanAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Generate(request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetActive_PlanExists_Returns200()
    {
        var plan = new MealPlanResponse { CreatedAt = DateTime.UtcNow, Id = Guid.NewGuid(), Name = "Active", Slots = [], WeekStartDate = DateOnly.FromDateTime(DateTime.Today) };
        _mockService.Setup(s => s.GetActiveMealPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _controller.GetActive();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(plan);
    }

    [Fact]
    public async Task GetActive_NoPlanExists_Returns404()
    {
        _mockService.Setup(s => s.GetActiveMealPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlanResponse?)null);

        var result = await _controller.GetActive();

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_PlanExists_Returns200()
    {
        var id = Guid.NewGuid();
        var plan = new MealPlanResponse { CreatedAt = DateTime.UtcNow, Id = id, Name = "Plan", Slots = [], WeekStartDate = DateOnly.FromDateTime(DateTime.Today) };
        _mockService.Setup(s => s.GetMealPlanAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _controller.GetById(id);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(plan);
    }

    [Fact]
    public async Task GetById_PlanNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetMealPlanAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlanResponse?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task List_ReturnsAllPlans()
    {
        var plans = new List<MealPlanResponse>
        {
            new() { CreatedAt = DateTime.UtcNow, Id = Guid.NewGuid(), Name = "Plan 1", Slots = [], WeekStartDate = DateOnly.FromDateTime(DateTime.Today) },
            new() { CreatedAt = DateTime.UtcNow, Id = Guid.NewGuid(), Name = "Plan 2", Slots = [], WeekStartDate = DateOnly.FromDateTime(DateTime.Today) }
        };
        _mockService.Setup(s => s.ListMealPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        var result = await _controller.List();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(plans);
    }

    [Fact]
    public async Task SwapSlot_SlotFound_Returns200()
    {
        var slotId = Guid.NewGuid();
        var request = new SwapSlotRequest { RecipeId = Guid.NewGuid() };
        var slot = new MealPlanSlotResponse { CuisineType = "Italian", DayOfWeek = DayOfWeek.Monday, Id = slotId, MealSlot = MealSlot.Dinner, RecipeId = request.RecipeId, RecipeTitle = "New Recipe" };
        _mockService.Setup(s => s.SwapSlotAsync(slotId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);

        var result = await _controller.SwapSlot(slotId, request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(slot);
    }

    [Fact]
    public async Task SwapSlot_SlotNotFound_Returns404()
    {
        var slotId = Guid.NewGuid();
        var request = new SwapSlotRequest { RecipeId = Guid.NewGuid() };
        _mockService.Setup(s => s.SwapSlotAsync(slotId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MealPlanSlotResponse?)null);

        var result = await _controller.SwapSlot(slotId, request);

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
