namespace MealsEnPlace.Api.Common;

/// <summary>
/// A generic paged result wrapper for list endpoints.
/// </summary>
/// <typeparam name="T">The item type in the page.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>The items in the current page.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>1-based index of the current page.</summary>
    public int Page { get; init; }

    /// <summary>Maximum number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Creates a <see cref="PagedResult{T}"/> from a pre-fetched item list.
    /// </summary>
    public static PagedResult<T> From(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
}
