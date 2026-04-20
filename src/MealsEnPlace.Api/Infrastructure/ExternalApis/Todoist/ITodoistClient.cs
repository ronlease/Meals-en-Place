namespace MealsEnPlace.Api.Infrastructure.ExternalApis.Todoist;

/// <summary>
/// Minimal Todoist REST API v2 surface used by the push targets (MEP-028 /
/// MEP-029). Covers create, update, and close — the three operations the
/// idempotent push flow needs. Deletion uses <see cref="CloseTaskAsync"/>
/// rather than the Todoist DELETE endpoint so that closed tasks still appear
/// in the user's "completed" history instead of vanishing.
/// </summary>
public interface ITodoistClient
{
    /// <summary>Marks an existing Todoist task as closed (completed).</summary>
    Task CloseTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>Creates a task and returns the provider-assigned task id.</summary>
    Task<string> CreateTaskAsync(
        TodoistTaskPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the content and/or due date of an existing Todoist task.</summary>
    Task UpdateTaskAsync(
        string taskId,
        TodoistTaskPayload payload,
        CancellationToken cancellationToken = default);
}

/// <summary>Request payload shared by create and update operations.</summary>
public sealed class TodoistTaskPayload
{
    /// <summary>The task title (e.g., "Dinner: Chicken Scampi").</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Optional ISO-8601 due date (<c>YYYY-MM-DD</c>). Null leaves no due date.</summary>
    public string? DueDate { get; init; }

    /// <summary>Optional Todoist project id. Null pushes to Inbox.</summary>
    public string? ProjectId { get; init; }
}
