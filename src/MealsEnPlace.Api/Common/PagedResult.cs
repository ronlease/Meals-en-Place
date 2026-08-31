namespace MealsEnPlace.Api.Common;

/// <summary>
/// Server-side pagination envelope returned by all paged list endpoints.
/// The <see cref="Items"/> collection contains only the rows for the current
/// page; callers use <see cref="TotalCount"/> and <see cref="TotalPages"/> to
/// drive pagination controls.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>The items on the current page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>The current page number (1-based).</summary>
    public int Page { get; init; }

    /// <summary>The number of items per page that was applied (after clamping).</summary>
    public int PageSize { get; init; }

    /// <summary>The total number of items across all pages.</summary>
    public long TotalCount { get; init; }

    /// <summary>The total number of pages given the current <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
