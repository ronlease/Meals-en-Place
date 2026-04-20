// Feature: Todoist Meal Plan Push Target (MEP-029)
//
// Scenario: ComputeDueDate places each slot on the correct calendar date relative to WeekStartDate
// Scenario: First push creates one task per slot and records links
// Scenario: Re-push with an unchanged slot is a no-op
// Scenario: Re-push after a slot's recipe was swapped updates the remote task and refreshes the hash
// Scenario: Re-push after a slot was removed closes the remote task and deletes the link
// Scenario: PushAsync throws InvalidOperationException when Todoist:Token is not configured
// Scenario: PushAsync throws when the meal plan id is unknown

using FluentAssertions;
using MealsEnPlace.Api.Features.MealPlan;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace MealsEnPlace.Unit.Features.MealPlan;

public sealed class TodoistMealPlanPushTargetTests : IDisposable
{
    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly Mock<ITodoistClient> _todoistMock = new(MockBehavior.Strict);

    public TodoistMealPlanPushTargetTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    [Theory]
    [InlineData("2026-05-04", DayOfWeek.Monday, "2026-05-04")]    // Monday start -> Monday = same day
    [InlineData("2026-05-04", DayOfWeek.Wednesday, "2026-05-06")] // Wednesday = start + 2
    [InlineData("2026-05-04", DayOfWeek.Sunday, "2026-05-10")]    // Sunday = last day of week
    public void ComputeDueDate_MondayStartWeek_MapsDayOfWeekToCalendarDate(
        string weekStartIso, DayOfWeek day, string expectedIso)
    {
        // Act
        var result = TodoistMealPlanPushTarget.ComputeDueDate(DateOnly.Parse(weekStartIso), day);

        // Assert
        result.Should().Be(expectedIso);
    }

    [Fact]
    public async Task PushAsync_FirstPush_CreatesOneTaskPerSlot()
    {
        // Arrange
        var plan = SeedPlanWithSlots(DateOnly.Parse("2026-05-04"),
            (DayOfWeek.Monday, MealSlot.Dinner, "Beef Stew"),
            (DayOfWeek.Tuesday, MealSlot.Dinner, "Pasta"));

        _todoistMock
            .SetupSequence(c => c.CreateTaskAsync(It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("remote-a")
            .ReturnsAsync("remote-b");

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(plan.Id);

        // Assert
        result.Created.Should().Be(2);
        result.Unchanged.Should().Be(0);
        (await _dbContext.ExternalTaskLinks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task PushAsync_RePushUnchanged_IsNoOp()
    {
        // Arrange
        var plan = SeedPlanWithSlots(DateOnly.Parse("2026-05-04"),
            (DayOfWeek.Monday, MealSlot.Dinner, "Beef Stew"));
        var slot = plan.Slots.Single();
        var content = $"Dinner: Beef Stew";
        _dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
        {
            ContentHash = TodoistMealPlanPushTarget.HashContent(content, "2026-05-04"),
            ExternalTaskId = "remote-existing",
            Id = Guid.NewGuid(),
            Provider = TodoistMealPlanPushTarget.TodoistProviderName,
            PushedAt = DateTime.UtcNow.AddDays(-1),
            SourceId = slot.Id,
            SourceScope = plan.Id.ToString(),
            SourceType = ExternalTaskSource.MealPlanSlot
        });
        await _dbContext.SaveChangesAsync();

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(plan.Id);

        // Assert — Todoist must not be called.
        result.Unchanged.Should().Be(1);
        result.Created.Should().Be(0);
        _todoistMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PushAsync_RecipeSwapped_UpdatesRemoteTask()
    {
        // Arrange — link exists for the slot but the slot's recipe was swapped.
        var plan = SeedPlanWithSlots(DateOnly.Parse("2026-05-04"),
            (DayOfWeek.Monday, MealSlot.Dinner, "Beef Stew"));
        var slot = plan.Slots.Single();
        _dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
        {
            // Stale hash — pretend the slot used to hold "Chicken Curry"
            ContentHash = TodoistMealPlanPushTarget.HashContent("Dinner: Chicken Curry", "2026-05-04"),
            ExternalTaskId = "remote-existing",
            Id = Guid.NewGuid(),
            Provider = TodoistMealPlanPushTarget.TodoistProviderName,
            PushedAt = DateTime.UtcNow.AddDays(-1),
            SourceId = slot.Id,
            SourceScope = plan.Id.ToString(),
            SourceType = ExternalTaskSource.MealPlanSlot
        });
        await _dbContext.SaveChangesAsync();

        _todoistMock
            .Setup(c => c.UpdateTaskAsync("remote-existing", It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(plan.Id);

        // Assert
        result.Updated.Should().Be(1);
        _todoistMock.Verify(
            c => c.UpdateTaskAsync("remote-existing", It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PushAsync_SlotRemoved_ClosesRemoteTask()
    {
        // Arrange
        var plan = SeedPlanWithSlots(DateOnly.Parse("2026-05-04"),
            (DayOfWeek.Monday, MealSlot.Dinner, "Beef Stew"));
        _dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
        {
            ContentHash = TodoistMealPlanPushTarget.HashContent("Dinner: Old Recipe", "2026-05-04"),
            ExternalTaskId = "remote-orphan",
            Id = Guid.NewGuid(),
            Provider = TodoistMealPlanPushTarget.TodoistProviderName,
            PushedAt = DateTime.UtcNow.AddDays(-1),
            SourceId = Guid.NewGuid(), // stale slot id that no longer exists
            SourceScope = plan.Id.ToString(),
            SourceType = ExternalTaskSource.MealPlanSlot
        });
        await _dbContext.SaveChangesAsync();

        _todoistMock
            .Setup(c => c.CloseTaskAsync("remote-orphan", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _todoistMock
            .Setup(c => c.CreateTaskAsync(It.IsAny<TodoistTaskPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("remote-new");

        var sut = BuildSut(token: "sample-token");

        // Act
        var result = await sut.PushAsync(plan.Id);

        // Assert
        result.Closed.Should().Be(1);
        result.Created.Should().Be(1);
        _todoistMock.Verify(c => c.CloseTaskAsync("remote-orphan", It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task PushAsync_UnknownMealPlan_ThrowsInvalidOperationException()
    {
        // Arrange
        var sut = BuildSut(token: "sample-token");

        // Act
        var act = async () => await sut.PushAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found*");
    }

    private TodoistMealPlanPushTarget BuildSut(string? token)
    {
        var options = Options.Create(new TodoistOptions { Token = token });
        return new TodoistMealPlanPushTarget(_dbContext, options, _todoistMock.Object);
    }

    private MealsEnPlace.Api.Models.Entities.MealPlan SeedPlanWithSlots(
        DateOnly weekStart,
        params (DayOfWeek Day, MealSlot MealSlot, string RecipeTitle)[] slotDefs)
    {
        var plan = new MealsEnPlace.Api.Models.Entities.MealPlan
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            WeekStartDate = weekStart
        };
        _dbContext.MealPlans.Add(plan);

        foreach (var (day, mealSlot, title) in slotDefs)
        {
            var recipe = new Recipe
            {
                CuisineType = "Test",
                Id = Guid.NewGuid(),
                Instructions = "Cook.",
                ServingCount = 1,
                Title = title
            };
            _dbContext.Recipes.Add(recipe);
            _dbContext.MealPlanSlots.Add(new MealPlanSlot
            {
                DayOfWeek = day,
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,
                MealSlot = mealSlot,
                RecipeId = recipe.Id
            });
        }

        _dbContext.SaveChanges();
        return plan;
    }
}
