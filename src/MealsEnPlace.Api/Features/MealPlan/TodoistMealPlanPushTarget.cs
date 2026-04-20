using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MealsEnPlace.Api.Features.MealPlan;

/// <summary>
/// Todoist implementation of <see cref="IMealPlanPushTarget"/>. Each
/// <see cref="MealPlanSlot"/> becomes one Todoist task titled
/// <c>"{MealSlot}: {RecipeTitle}"</c> and scheduled for that slot's calendar
/// date (plan's <c>WeekStartDate</c> shifted to the slot's <c>DayOfWeek</c>).
/// Re-push is idempotent via the shared <see cref="ExternalTaskLink"/>
/// keyed by (slot id, scope=mealPlanId).
/// </summary>
public sealed class TodoistMealPlanPushTarget(
    MealsEnPlaceDbContext dbContext,
    IOptions<TodoistOptions> options,
    ITodoistClient todoistClient) : IMealPlanPushTarget
{
    public const string TodoistProviderName = "Todoist";

    public string ProviderName => TodoistProviderName;

    public async Task<MealPlanPushResult> PushAsync(
        Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured)
        {
            throw new InvalidOperationException("Todoist integration is not configured. Set the Todoist:Token user secret.");
        }

        var plan = await dbContext.MealPlans
            .AsNoTracking()
            .Include(mp => mp.Slots)
                .ThenInclude(s => s.Recipe)
            .FirstOrDefaultAsync(mp => mp.Id == mealPlanId, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException($"Meal plan '{mealPlanId}' was not found.");
        }

        var scope = mealPlanId.ToString();
        var projectId = string.IsNullOrWhiteSpace(options.Value.ProjectId) ? null : options.Value.ProjectId;

        var links = await dbContext.ExternalTaskLinks
            .Where(l => l.Provider == TodoistProviderName
                && l.SourceType == ExternalTaskSource.MealPlanSlot
                && l.SourceScope == scope)
            .ToListAsync(cancellationToken);
        var linksBySlotId = links.ToDictionary(l => l.SourceId);

        var created = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var slot in plan.Slots)
        {
            var content = BuildContent(slot);
            var dueDate = ComputeDueDate(plan.WeekStartDate, slot.DayOfWeek);
            var hash = HashContent(content, dueDate);
            var payload = new TodoistTaskPayload
            {
                Content = content,
                DueDate = dueDate,
                ProjectId = projectId
            };

            if (linksBySlotId.TryGetValue(slot.Id, out var existingLink))
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
                    SourceId = slot.Id,
                    SourceScope = scope,
                    SourceType = ExternalTaskSource.MealPlanSlot
                });
                created++;
            }
        }

        var closed = await CloseOrphanedLinksAsync(plan.Slots, linksBySlotId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new MealPlanPushResult
        {
            Closed = closed,
            Created = created,
            Unchanged = unchanged,
            Updated = updated
        };
    }

    internal static string BuildContent(MealPlanSlot slot)
    {
        var recipeTitle = slot.Recipe?.Title ?? "(no recipe)";
        return $"{slot.MealSlot}: {recipeTitle}";
    }

    internal static string ComputeDueDate(DateOnly weekStart, DayOfWeek targetDay)
    {
        // The existing meal plan generation code uses Monday-first ordering
        // (see MealPlanService.DayOrderIndex). Mirror that here so the
        // plan's Monday slot falls on WeekStartDate, Tuesday on +1, etc.
        var startIndex = DayOrderIndex(weekStart.DayOfWeek);
        var slotIndex = DayOrderIndex(targetDay);
        var offset = slotIndex - startIndex;
        if (offset < 0)
        {
            offset += 7;
        }

        var date = weekStart.AddDays(offset);
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    internal static string HashContent(string content, string dueDate)
    {
        var bytes = Encoding.UTF8.GetBytes($"{content}\u001f{dueDate}");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private async Task<int> CloseOrphanedLinksAsync(
        ICollection<MealPlanSlot> currentSlots,
        Dictionary<Guid, ExternalTaskLink> linksBySlotId,
        CancellationToken cancellationToken)
    {
        var currentIds = currentSlots.Select(s => s.Id).ToHashSet();
        var orphaned = linksBySlotId.Values
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

    private static int DayOrderIndex(DayOfWeek day) => ((int)day + 6) % 7;
}
