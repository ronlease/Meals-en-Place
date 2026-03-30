// Feature: Shopping List Controller (meal-plan-scoped)
//
// Scenario: Generate shopping list — returns 200 with items
//   Given a valid meal plan ID
//   When POST /api/v1/meal-plans/{mealPlanId}/shopping-list is called
//   Then the response is 200 OK with the generated shopping list items
//
// Scenario: Generate shopping list — returns 200 with empty list when nothing needed
//   Given all required ingredients are in inventory
//   When POST /api/v1/meal-plans/{mealPlanId}/shopping-list is called
//   Then the response is 200 OK with an empty list
//
// Scenario: Get shopping list — returns 200 with existing items
//   Given a shopping list has already been generated for a meal plan
//   When GET /api/v1/meal-plans/{mealPlanId}/shopping-list is called
//   Then the response is 200 OK with the shopping list items
//
// Scenario: Get shopping list — returns 200 with empty list when none exist
//   Given no shopping list exists for the meal plan
//   When GET /api/v1/meal-plans/{mealPlanId}/shopping-list is called
//   Then the response is 200 OK with an empty list

using FluentAssertions;
using MealsEnPlace.Api.Features.ShoppingList;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.ShoppingList;

public class ShoppingListControllerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly ShoppingListController _sut;
    private readonly Mock<IShoppingListService> _serviceMock = new(MockBehavior.Strict);

    public ShoppingListControllerTests()
    {
        _sut = new ShoppingListController(_serviceMock.Object);
    }

    // ── Generate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_ValidMealPlanId_Returns200WithItems()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid();
        var items = BuildShoppingListItems(2);
        _serviceMock
            .Setup(s => s.GenerateShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _sut.Generate(mealPlanId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task Generate_AllIngredientsInInventory_Returns200WithEmptyList()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GenerateShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.Generate(mealPlanId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.As<List<ShoppingListItemResponse>>().Should().BeEmpty();
    }

    [Fact]
    public async Task Generate_CallsGenerateShoppingListAsyncOnce()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GenerateShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.Generate(mealPlanId, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.GenerateShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ShoppingListExists_Returns200WithItems()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid();
        var items = BuildShoppingListItems(3);
        _serviceMock
            .Setup(s => s.GetShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _sut.Get(mealPlanId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task Get_NoShoppingListExists_Returns200WithEmptyList()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GetShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.Get(mealPlanId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.As<List<ShoppingListItemResponse>>().Should().BeEmpty();
    }

    [Fact]
    public async Task Get_CallsGetShoppingListAsyncOnce()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.GetShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.Get(mealPlanId, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.GetShoppingListAsync(mealPlanId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static List<ShoppingListItemResponse> BuildShoppingListItems(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new ShoppingListItemResponse
            {
                CanonicalIngredientName = "Tomatoes",
                Category = IngredientCategory.Produce,
                Id = Guid.NewGuid(),
                Quantity = 400m,
                UomAbbreviation = "g"
            })
            .ToList();
}
