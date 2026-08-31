using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Features.Settings;

/// <inheritdoc cref="ITodoistProjectHistoryService"/>
public sealed class TodoistProjectHistoryService(
    MealsEnPlaceDbContext dbContext,
    ITodoistProjectClient projectClient,
    ITodoistTokenResolver tokenResolver) : ITodoistProjectHistoryService
{
    private const string InboxDisplayName = "Inbox (default)";
    private const string TodoistProviderName = "Todoist";

    public async Task<TodoistProjectHistoryResponse> GetProjectHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var historicIds = await FetchDistinctHistoryIdsAsync(cancellationToken);
        var lastUsedMealPlan = await FetchLastUsedProjectIdAsync(ExternalTaskSource.MealPlanSlot, cancellationToken);
        var lastUsedShoppingList = await FetchLastUsedProjectIdAsync(ExternalTaskSource.ShoppingListItem, cancellationToken);

        var token = await tokenResolver.ResolveAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return BuildResponse(
                historicIds,
                liveProjects: [],
                namesResolved: false,
                nameResolutionError: "No Todoist token is configured.",
                lastUsedMealPlan,
                lastUsedShoppingList);
        }

        var result = await projectClient.GetProjectsAsync(token, cancellationToken);
        return BuildResponse(
            historicIds,
            liveProjects: result.Projects,
            namesResolved: result.Succeeded,
            nameResolutionError: result.Succeeded ? null : result.ErrorMessage,
            lastUsedMealPlan,
            lastUsedShoppingList);
    }

    private static TodoistProjectHistoryResponse BuildResponse(
        IReadOnlyList<string> historicIds,
        IReadOnlyList<TodoistProject> liveProjects,
        bool namesResolved,
        string? nameResolutionError,
        string? lastUsedMealPlan,
        string? lastUsedShoppingList)
    {
        var nameByProjectId = liveProjects.ToDictionary(p => p.Id, p => p.Name);

        var inbox = new TodoistProjectHistoryEntry
        {
            DisplayName = InboxDisplayName,
            IsInbox = true,
            ProjectId = null
        };

        var historyEntries = historicIds
            .Select(id => new TodoistProjectHistoryEntry
            {
                DisplayName = nameByProjectId.GetValueOrDefault(id),
                IsInbox = false,
                ProjectId = id
            })
            .ToList();

        var allEntries = new List<TodoistProjectHistoryEntry> { inbox };
        allEntries.AddRange(historyEntries);

        return new TodoistProjectHistoryResponse
        {
            LastUsedMealPlanProjectId = lastUsedMealPlan,
            LastUsedShoppingListProjectId = lastUsedShoppingList,
            NameResolutionError = nameResolutionError,
            NamesResolved = namesResolved,
            Projects = allEntries
        };
    }

    private async Task<IReadOnlyList<string>> FetchDistinctHistoryIdsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.ExternalTaskLinks
            .Where(l => l.Provider == TodoistProviderName && l.ExternalProjectId != null)
            .Select(l => l.ExternalProjectId!)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<string?> FetchLastUsedProjectIdAsync(
        ExternalTaskSource sourceType, CancellationToken cancellationToken)
    {
        // Returns the ExternalProjectId from the most recently pushed link for
        // this source type. Null means either no history or the last push was
        // to Inbox — both correctly map to "pre-select Inbox" in the dialog.
        return await dbContext.ExternalTaskLinks
            .Where(l => l.Provider == TodoistProviderName && l.SourceType == sourceType)
            .OrderByDescending(l => l.PushedAt)
            .Select(l => l.ExternalProjectId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
