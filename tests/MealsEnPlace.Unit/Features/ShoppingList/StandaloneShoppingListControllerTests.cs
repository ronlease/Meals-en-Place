// Feature: Standalone Shopping List Controller
//
// Scenario: Add from recipe — returns 200 with updated list
//   Given a valid recipe ID
//   When POST /api/v1/shopping-list/add-from-recipe/{recipeId} is called
//   Then the response is 200 OK with the updated standalone shopping list
//
// Scenario: Add from recipe — returns 200 with empty list when all ingredients in inventory
//   Given all recipe ingredients are covered by inventory
//   When POST /api/v1/shopping-list/add-from-recipe/{recipeId} is called
//   Then the response is 200 OK with an empty list
//
// Scenario: Get standalone list — returns 200 with items
//   Given standalone shopping list items exist
//   When GET /api/v1/shopping-list is called
//   Then the response is 200 OK with the items
//
// Scenario: Get standalone list — returns 200 with empty list when none exist
//   Given no standalone shopping list items exist
//   When GET /api/v1/shopping-list is called
//   Then the response is 200 OK with an empty list

using FluentAssertions;
using MealsEnPlace.Api.Features.ShoppingList;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MealsEnPlace.Unit.Features.ShoppingList;

public class StandaloneShoppingListControllerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IShoppingListService> _serviceMock = new(MockBehavior.Strict);
    private readonly StandaloneShoppingListController _sut;

    public StandaloneShoppingListControllerTests()
    {
        _sut = new StandaloneShoppingListController(_serviceMock.Object);
    }

    // ── AddFromRecipe ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddFromRecipe_ValidRecipeId_Returns200WithUpdatedList()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var items = BuildShoppingListItems(2);
        _serviceMock
            .Setup(s => s.AddFromRecipeAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _sut.AddFromRecipe(recipeId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task AddFromRecipe_AllIngredientsInInventory_Returns200WithEmptyList()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.AddFromRecipeAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.AddFromRecipe(recipeId, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.As<List<ShoppingListItemResponse>>().Should().BeEmpty();
    }

    [Fact]
    public async Task AddFromRecipe_CallsAddFromRecipeAsyncOnce()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        _serviceMock
            .Setup(s => s.AddFromRecipeAsync(recipeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.AddFromRecipe(recipeId, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.AddFromRecipeAsync(recipeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ItemsExist_Returns200WithItems()
    {
        // Arrange
        var items = BuildShoppingListItems(3);
        _serviceMock
            .Setup(s => s.GetStandaloneShoppingListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _sut.Get(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task Get_NoItemsExist_Returns200WithEmptyList()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetStandaloneShoppingListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.Get(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.As<List<ShoppingListItemResponse>>().Should().BeEmpty();
    }

    [Fact]
    public async Task Get_CallsGetStandaloneShoppingListAsyncOnce()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetStandaloneShoppingListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _sut.Get(CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.GetStandaloneShoppingListAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static List<ShoppingListItemResponse> BuildShoppingListItems(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => new ShoppingListItemResponse
            {
                CanonicalIngredientName = "Chicken Breast",
                Category = IngredientCategory.Protein,
                Id = Guid.NewGuid(),
                Quantity = 250m,
                UnitOfMeasureAbbreviation = "g"
            })
            .ToList();
}
