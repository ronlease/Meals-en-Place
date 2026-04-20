// Feature: Todoist Shopping List Push Target (MEP-028)
//
// Scenario: Happy path first push creates one task per item and records a link
// Scenario: Re-push with unchanged content is a no-op (Todoist is never touched)
// Scenario: Re-push with quantity change updates the existing task
// Scenario: Re-push after an item was removed closes the corresponding task and deletes the link
// Scenario: PushAsync throws InvalidOperationException when Todoist:Token is not configured

using FluentAssertions;
using MealsEnPlace.Api.Features.ShoppingList;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.Data.Configurations;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace MealsEnPlace.Unit.Features.ShoppingList;

public sealed class TodoistShoppingListPushTargetTests : IDisposable
{
    private static readonly Guid GramId = UnitOfMeasureConfiguration.GramId;

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly Mock<ITodoistClient> _todoistMock = new(MockBehavior.Strict);

    public TodoistShoppingListPushTargetTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        SeedGramUnit();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task PushAsync_FirstPush_CreatesTaskPerItemAndRecordsLink()
    {
        // Arrange
        var mealPlanId = Guid.NewGuid();
        var flour = SeedIngredient("Flour");
        var sugar = SeedIngredient("Sugar");
        SeedShoppingListItem(mealPlanId, flour.Id, 500m);
        SeedShoppingListItem(mealPlanId, sugar.Id, 200m);

        _todoistMock
            .SetupSequence(c => c.CreateTaskAsync(It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("remote-1")
            .ReturnsAsync("remote-2");

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(mealPlanId);

        // Assert
        result.Created.Should().Be(2);
        result.Updated.Should().Be(0);
        result.Unchanged.Should().Be(0);
        result.Closed.Should().Be(0);
        (await _dbContext.ExternalTaskLinks.CountAsync()).Should().Be(2);
        _todoistMock.Verify(
            c => c.CreateTaskAsync(It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PushAsync_RePushUnchanged_DoesNotInvokeTodoist()
    {
        // Arrange — seed an existing link with the same content hash we'd compute now
        var mealPlanId = Guid.NewGuid();
        var flour = SeedIngredient("Flour");
        var item = SeedShoppingListItem(mealPlanId, flour.Id, 500m);
        var content = $"500 g Flour";
        _dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
        {
            ContentHash = TodoistShoppingListPushTarget.HashContent(content),
            ExternalTaskId = "remote-existing",
            Id = Guid.NewGuid(),
            Provider = TodoistShoppingListPushTarget.TodoistProviderName,
            PushedAt = DateTime.UtcNow.AddDays(-1),
            SourceId = item.Id,
            SourceScope = mealPlanId.ToString(),
            SourceType = ExternalTaskSource.ShoppingListItem
        });
        await _dbContext.SaveChangesAsync();

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(mealPlanId);

        // Assert — no Todoist calls of any kind, no new or removed links.
        result.Unchanged.Should().Be(1);
        result.Created.Should().Be(0);
        result.Updated.Should().Be(0);
        result.Closed.Should().Be(0);
        _todoistMock.VerifyNoOtherCalls();
        (await _dbContext.ExternalTaskLinks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PushAsync_ContentChanged_UpdatesRemoteTaskAndHash()
    {
        // Arrange — existing link but with a stale hash (quantity was 300g before).
        var mealPlanId = Guid.NewGuid();
        var flour = SeedIngredient("Flour");
        var item = SeedShoppingListItem(mealPlanId, flour.Id, 500m);
        _dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
        {
            ContentHash = TodoistShoppingListPushTarget.HashContent("300 g Flour"),
            ExternalTaskId = "remote-existing",
            Id = Guid.NewGuid(),
            Provider = TodoistShoppingListPushTarget.TodoistProviderName,
            PushedAt = DateTime.UtcNow.AddDays(-1),
            SourceId = item.Id,
            SourceScope = mealPlanId.ToString(),
            SourceType = ExternalTaskSource.ShoppingListItem
        });
        await _dbContext.SaveChangesAsync();

        _todoistMock
            .Setup(c => c.UpdateTaskAsync("remote-existing", It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(mealPlanId);

        // Assert
        result.Updated.Should().Be(1);
        _todoistMock.Verify(
            c => c.UpdateTaskAsync("remote-existing", It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Hash should now match the new content.
        var updated = await _dbContext.ExternalTaskLinks.SingleAsync();
        updated.ContentHash.Should().Be(TodoistShoppingListPushTarget.HashContent("500 g Flour"));
    }

    [Fact]
    public async Task PushAsync_ItemRemoved_ClosesTaskAndDeletesLink()
    {
        // Arrange — link exists but the source item has been deleted.
        var mealPlanId = Guid.NewGuid();
        var orphanSourceId = Guid.NewGuid();
        _dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
        {
            ContentHash = TodoistShoppingListPushTarget.HashContent("100 g GoneIngredient"),
            ExternalTaskId = "remote-orphan",
            Id = Guid.NewGuid(),
            Provider = TodoistShoppingListPushTarget.TodoistProviderName,
            PushedAt = DateTime.UtcNow.AddDays(-1),
            SourceId = orphanSourceId,
            SourceScope = mealPlanId.ToString(),
            SourceType = ExternalTaskSource.ShoppingListItem
        });
        await _dbContext.SaveChangesAsync();

        _todoistMock
            .Setup(c => c.CloseTaskAsync("remote-orphan", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(mealPlanId);

        // Assert
        result.Closed.Should().Be(1);
        _todoistMock.Verify(c => c.CloseTaskAsync("remote-orphan", It.IsAny<CancellationToken>()), Times.Once);
        (await _dbContext.ExternalTaskLinks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PushAsync_Unconfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = BuildSut(token: null);

        // Act
        var act = async () => await sut.PushAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    private TodoistShoppingListPushTarget BuildSut(string? token)
    {
        var options = Options.Create(new TodoistOptions { Token = token });
        return new TodoistShoppingListPushTarget(_dbContext, options, _todoistMock.Object);
    }

    private void SeedGramUnit()
    {
        _dbContext.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1m,
            Id = GramId,
            Name = "Gram",
            UnitOfMeasureType = UnitOfMeasureType.Weight
        });
        _dbContext.SaveChanges();
    }

    private CanonicalIngredient SeedIngredient(string name)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Other,
            DefaultUnitOfMeasureId = GramId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        _dbContext.SaveChanges();
        return ingredient;
    }

    private ShoppingListItem SeedShoppingListItem(Guid mealPlanId, Guid canonicalIngredientId, decimal quantity)
    {
        var item = new ShoppingListItem
        {
            CanonicalIngredientId = canonicalIngredientId,
            Id = Guid.NewGuid(),
            MealPlanId = mealPlanId,
            Quantity = quantity,
            UnitOfMeasureId = GramId
        };
        _dbContext.ShoppingListItems.Add(item);
        _dbContext.SaveChanges();
        return item;
    }
}
