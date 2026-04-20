using System.Security.Cryptography;
using System.Text;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MealsEnPlace.Api.Features.ShoppingList;

/// <summary>
/// Todoist implementation of <see cref="IShoppingListPushTarget"/>. Uses the
/// <see cref="ExternalTaskLink"/> table for idempotency — each
/// <see cref="ShoppingListItem"/> is keyed by a stable source id + scope so
/// re-pushing reconciles deltas instead of creating duplicates.
/// </summary>
public sealed class TodoistShoppingListPushTarget(
    MealsEnPlaceDbContext dbContext,
    IOptions<TodoistOptions> options,
    ITodoistClient todoistClient) : IShoppingListPushTarget
{
    private const string StandaloneScopeKey = "standalone";
    public const string TodoistProviderName = "Todoist";

    public string ProviderName => TodoistProviderName;

    public async Task<ShoppingListPushResult> PushAsync(
        Guid? mealPlanId, CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured)
        {
            throw new InvalidOperationException("Todoist integration is not configured. Set the Todoist:Token user secret.");
        }

        var scope = mealPlanId?.ToString() ?? StandaloneScopeKey;
        var projectId = string.IsNullOrWhiteSpace(options.Value.ProjectId) ? null : options.Value.ProjectId;

        var items = await LoadItemsAsync(mealPlanId, cancellationToken);
        var links = await dbContext.ExternalTaskLinks
            .Where(l => l.Provider == TodoistProviderName
                && l.SourceType == ExternalTaskSource.ShoppingListItem
                && l.SourceScope == scope)
            .ToListAsync(cancellationToken);
        var linksBySourceId = links.ToDictionary(l => l.SourceId);

        var created = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var item in items)
        {
            var content = BuildContent(item);
            var hash = HashContent(content);
            var payload = new TodoistTaskPayload
            {
                Content = content,
                ProjectId = projectId
            };

            if (linksBySourceId.TryGetValue(item.Id, out var existingLink))
            {
                if (existingLink.ContentHash == hash)
                {
                    unchanged++;
                }
                else
                {
                    await todoistClient.UpdateTaskAsync(existingLink.ExternalTaskId, payload, cancellationToken);
                    existingLink.ContentHash = hash;
                    existingLink.ExternalProjectId = projectId;
                    existingLink.PushedAt = DateTime.UtcNow;
                    updated++;
                }
            }
            else
            {
                var externalTaskId = await todoistClient.CreateTaskAsync(payload, cancellationToken);
                dbContext.ExternalTaskLinks.Add(new ExternalTaskLink
                {
                    ContentHash = hash,
                    ExternalProjectId = projectId,
                    ExternalTaskId = externalTaskId,
                    Id = Guid.NewGuid(),
                    Provider = TodoistProviderName,
                    PushedAt = DateTime.UtcNow,
                    SourceId = item.Id,
                    SourceScope = scope,
                    SourceType = ExternalTaskSource.ShoppingListItem
                });
                created++;
            }
        }

        var closed = await CloseOrphanedLinksAsync(items, linksBySourceId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ShoppingListPushResult
        {
            Closed = closed,
            Created = created,
            Unchanged = unchanged,
            Updated = updated
        };
    }

    /// <summary>
    /// Produces the task title. The shape matches MEP-028's AC:
    /// <c>"{Quantity} {UomAbbreviation} {IngredientName}"</c>.
    /// </summary>
    internal static string BuildContent(ShoppingListItem item)
    {
        var ingredientName = item.CanonicalIngredient?.Name ?? string.Empty;
        var abbreviation = item.UnitOfMeasure?.Abbreviation ?? string.Empty;
        return $"{item.Quantity} {abbreviation} {ingredientName}".Trim();
    }

    internal static string HashContent(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private async Task<int> CloseOrphanedLinksAsync(
        List<ShoppingListItem> currentItems,
        Dictionary<Guid, ExternalTaskLink> linksBySourceId,
        CancellationToken cancellationToken)
    {
        var currentIds = currentItems.Select(i => i.Id).ToHashSet();
        var orphaned = linksBySourceId.Values
            .Where(l => !currentIds.Contains(l.SourceId))
            .ToList();

        foreach (var link in orphaned)
        {
            try
            {
                await todoistClient.CloseTaskAsync(link.ExternalTaskId, cancellationToken);
            }
            catch (TodoistApiException ex) when (ex.StatusCode == 404)
            {
                // Task already gone on the Todoist side — drop the local link anyway.
            }
            dbContext.ExternalTaskLinks.Remove(link);
        }

        return orphaned.Count;
    }

    private async Task<List<ShoppingListItem>> LoadItemsAsync(
        Guid? mealPlanId, CancellationToken cancellationToken)
    {
        var query = dbContext.ShoppingListItems
            .Include(i => i.CanonicalIngredient)
            .Include(i => i.UnitOfMeasure)
            .AsQueryable();

        query = mealPlanId.HasValue
            ? query.Where(i => i.MealPlanId == mealPlanId.Value)
            : query.Where(i => i.MealPlanId == null);

        return await query
            .OrderBy(i => i.CanonicalIngredient!.Name)
            .ToListAsync(cancellationToken);
    }
}
