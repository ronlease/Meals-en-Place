// Feature: Todoist Project History Service (MEP-036)
//
// Scenario: Returns Inbox plus resolved history entries when token and projects are available
// Scenario: Returns Inbox only when no ExternalTaskLink rows exist for Todoist
// Scenario: Returns entries with null display names when project client call fails
// Scenario: Returns entries with null display names when no token is configured
// Scenario: NamesResolved is false and NameResolutionError is set when token is missing
// Scenario: NamesResolved is false and NameResolutionError is set when project client fails
// Scenario: Duplicate project IDs across multiple ExternalTaskLink rows collapse to a single history entry
// Scenario: LastUsedShoppingListProjectId reflects the most recent ShoppingListItem push
// Scenario: LastUsedMealPlanProjectId reflects the most recent MealPlanSlot push
// Scenario: LastUsedProjectId is null when no links exist for that source type
// Scenario: Last-used project ID is independent per source type even with interleaved rows
// Scenario: A historic project ID not in the live list still appears in the result
// Scenario: Inbox is always the first entry regardless of history
// Scenario: Project client is called exactly once regardless of how many distinct history IDs exist

using FluentAssertions;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MealsEnPlace.Unit.Features.Settings;

public sealed class TodoistProjectHistoryServiceTests : IDisposable
{
    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly Mock<ITodoistProjectClient> _projectClientMock = new(MockBehavior.Strict);
    private readonly Mock<ITodoistTokenResolver> _tokenResolverMock = new(MockBehavior.Strict);

    public TodoistProjectHistoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task GetProjectHistoryAsync_WithTokenAndHistory_ReturnsInboxPlusResolvedEntries()
    {
        SeedLink("2331547980", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-1));
        SeedLink("9988776655", ExternalTaskSource.MealPlanSlot, DateTime.UtcNow.AddDays(-2));
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult
            {
                Succeeded = true,
                Projects = new[]
                {
                    new TodoistProject { Id = "2331547980", Name = "Groceries" },
                    new TodoistProject { Id = "9988776655", Name = "Meals" }
                }
            });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.NamesResolved.Should().BeTrue();
        result.NameResolutionError.Should().BeNull();
        result.Projects.Should().HaveCount(3);
        result.Projects[0].IsInbox.Should().BeTrue();
        result.Projects[0].DisplayName.Should().Be("Inbox (default)");
        result.Projects[0].ProjectId.Should().BeNull();
        result.Projects.Skip(1).Should().AllSatisfy(e => e.IsInbox.Should().BeFalse());
    }

    [Fact]
    public async Task GetProjectHistoryAsync_NoHistory_ReturnsInboxOnly()
    {
        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.Projects.Should().HaveCount(1);
        result.Projects[0].IsInbox.Should().BeTrue();
        result.NamesResolved.Should().BeTrue();
    }

    [Fact]
    public async Task GetProjectHistoryAsync_ProjectClientFails_ReturnsNullDisplayNamesWithFlag()
    {
        SeedLink("2331547980", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult
            {
                Succeeded = false,
                ErrorMessage = "Network error contacting Todoist: timeout"
            });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.NamesResolved.Should().BeFalse();
        result.NameResolutionError.Should().Contain("timeout");
        result.Projects.Should().HaveCount(2);
        var historyEntry = result.Projects[1];
        historyEntry.ProjectId.Should().Be("2331547980");
        historyEntry.DisplayName.Should().BeNull();
        historyEntry.IsInbox.Should().BeFalse();
    }

    [Fact]
    public async Task GetProjectHistoryAsync_NoToken_ReturnsNullDisplayNamesWithFlag()
    {
        SeedLink("2331547980", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        _tokenResolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await BuildSut().GetProjectHistoryAsync();

        result.NamesResolved.Should().BeFalse();
        result.NameResolutionError.Should().NotBeNullOrWhiteSpace();
        result.Projects.Should().HaveCount(2);
        result.Projects[1].DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task GetProjectHistoryAsync_LastUsedShoppingList_ReflectsMostRecentPush()
    {
        SeedLink("111", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-2));
        SeedLink("222", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.LastUsedShoppingListProjectId.Should().Be("222");
    }

    [Fact]
    public async Task GetProjectHistoryAsync_LastUsedMealPlan_ReflectsMostRecentPush()
    {
        SeedLink("333", ExternalTaskSource.MealPlanSlot, DateTime.UtcNow.AddDays(-5));
        SeedLink("444", ExternalTaskSource.MealPlanSlot, DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.LastUsedMealPlanProjectId.Should().Be("444");
    }

    [Fact]
    public async Task GetProjectHistoryAsync_NoLinksForSourceType_LastUsedIsNull()
    {
        // Only ShoppingListItem links — no MealPlanSlot links
        SeedLink("2331547980", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.LastUsedMealPlanProjectId.Should().BeNull();
        result.LastUsedShoppingListProjectId.Should().Be("2331547980");
    }

    [Fact]
    public async Task GetProjectHistoryAsync_DuplicateProjectIds_CollapsesToSingleEntry()
    {
        // Arrange — three ExternalTaskLink rows all referencing the same project ID.
        // The distinct-ID query must collapse them to one history entry.
        SeedLink("2331547980", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-3));
        SeedLink("2331547980", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-2));
        SeedLink("2331547980", ExternalTaskSource.MealPlanSlot, DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        // Act
        var result = await BuildSut().GetProjectHistoryAsync();

        // Assert — Inbox plus exactly one entry for "2331547980", not three
        result.Projects.Should().HaveCount(2);
        result.Projects.Count(p => p.ProjectId == "2331547980").Should().Be(1);
    }

    [Fact]
    public async Task GetProjectHistoryAsync_HistoricIdNotInLiveList_StillAppearsWithNullName()
    {
        SeedLink("stale-id", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult
            {
                Succeeded = true,
                Projects = [new TodoistProject { Id = "other-id", Name = "Other" }]
            });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.NamesResolved.Should().BeTrue();
        var staleEntry = result.Projects.FirstOrDefault(p => p.ProjectId == "stale-id");
        staleEntry.Should().NotBeNull();
        staleEntry!.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task GetProjectHistoryAsync_InboxIsAlwaysFirst()
    {
        SeedLink("2331547980", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        var result = await BuildSut().GetProjectHistoryAsync();

        result.Projects[0].IsInbox.Should().BeTrue();
    }

    [Fact]
    public async Task GetProjectHistoryAsync_InterleavedSourceTypes_LastUsedIsIndependentPerType()
    {
        // Arrange — four rows interleaved across both source types.
        // Timeline (oldest first): MP@-5 → SL@-3 → MP@-2 → SL@-1
        // The globally newest row is SL@-1, but MP's newest is MP@-2.
        // An ordering bug (e.g. ordering all rows together before partitioning) would
        // make LastUsedMealPlanProjectId return "SL-latest" instead of "MP-latest".
        SeedLink("MP-early", ExternalTaskSource.MealPlanSlot, DateTime.UtcNow.AddDays(-5));
        SeedLink("SL-early", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-3));
        SeedLink("MP-latest", ExternalTaskSource.MealPlanSlot, DateTime.UtcNow.AddDays(-2));
        SeedLink("SL-latest", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        // Act
        var result = await BuildSut().GetProjectHistoryAsync();

        // Assert — each source type independently picks its own most-recent row
        result.LastUsedShoppingListProjectId.Should().Be("SL-latest");
        result.LastUsedMealPlanProjectId.Should().Be("MP-latest");
    }

    [Fact]
    public async Task GetProjectHistoryAsync_MultipleDistinctHistoryIds_CallsProjectClientOnce()
    {
        // Arrange — three distinct project IDs; the service must issue one GET /rest/v2/projects
        // call and resolve names in bulk rather than one call per ID.
        SeedLink("AAA", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-3));
        SeedLink("BBB", ExternalTaskSource.ShoppingListItem, DateTime.UtcNow.AddDays(-2));
        SeedLink("CCC", ExternalTaskSource.MealPlanSlot, DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        SetupToken("tok");
        _projectClientMock
            .Setup(c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistProjectListResult { Succeeded = true, Projects = [] });

        // Act
        await BuildSut().GetProjectHistoryAsync();

        // Assert — exactly one round-trip to the Todoist projects API
        _projectClientMock.Verify(
            c => c.GetProjectsAsync("tok", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private TodoistProjectHistoryService BuildSut() =>
        new(_dbContext, _projectClientMock.Object, _tokenResolverMock.Object);

    private void SeedLink(string? projectId, ExternalTaskSource sourceType, DateTime pushedAt)
    {
        _dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
        {
            ContentHash = "abc",
            ExternalProjectId = projectId,
            ExternalTaskId = Guid.NewGuid().ToString(),
            Id = Guid.NewGuid(),
            Provider = "Todoist",
            PushedAt = pushedAt,
            SourceId = Guid.NewGuid(),
            SourceScope = "scope",
            SourceType = sourceType
        });
    }

    private void SetupToken(string token)
    {
        _tokenResolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
    }
}
