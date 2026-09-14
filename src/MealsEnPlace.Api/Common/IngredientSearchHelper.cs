using MealsEnPlace.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// Database-provider-agnostic helper for executing ingredient name searches.
/// </summary>
/// <remarks>
/// <para>
/// <c>EF.Functions.ILike</c> is specific to the Npgsql provider and throws a
/// <see cref="NotSupportedException"/> when used against the EF Core in-memory
/// provider that unit tests rely on.  This class inspects
/// <c>db.Database.ProviderName</c> and routes to <c>ILike</c> on PostgreSQL,
/// falling back to a client-compatible <c>ToLower().Contains()</c> expression
/// otherwise, so that production and test code paths both work without
/// duplicating controller logic.
/// </para>
/// </remarks>
internal static class IngredientSearchHelper
{
    /// <summary>
    /// Returns a queryable that filters <paramref name="source"/> to rows whose
    /// <c>Name</c> contains <paramref name="search"/> (case-insensitive) and applies
    /// a three-level sort: prefix matches first, then by descending
    /// <see cref="CanonicalIngredient.RecipeReferenceCount"/> so that frequently-used
    /// ingredients surface above low-quality fragments, then by name ascending.
    /// Results are limited to <paramref name="limit"/> rows.
    /// </summary>
    /// <remarks>
    /// On Npgsql (PostgreSQL) the name filter and prefix-sort use <c>ILike</c> for
    /// server-side case-insensitive matching.  On the EF Core in-memory provider
    /// the equivalent <c>ToLower().Contains()</c> / <c>ToLower().StartsWith()</c>
    /// expressions are used instead.  The
    /// <see cref="CanonicalIngredient.RecipeReferenceCount"/> column is read directly
    /// in both branches — it is a stored, indexed value, not a correlated subquery.
    /// The <paramref name="search"/> term must not be null or whitespace; callers are
    /// responsible for that guard.
    /// </remarks>
    /// <param name="source">
    /// The base queryable to filter; should already have <c>AsNoTracking</c> applied.
    /// </param>
    /// <param name="providerName">
    /// The EF Core database provider name, obtained from <c>db.Database.ProviderName</c>.
    /// </param>
    /// <param name="search">The non-empty, trimmed search term.</param>
    /// <param name="limit">Maximum rows to return; must already be clamped to a safe range.</param>
    internal static IQueryable<CanonicalIngredient> ApplySearch(
        IQueryable<CanonicalIngredient> source,
        string providerName,
        string search,
        int limit)
    {
        if (providerName == NpgsqlProviderName)
        {
            var escaped = EscapeILikeWildcards(search);
            return source
                .Where(c => EF.Functions.ILike(c.Name, "%" + escaped + "%"))
                .OrderBy(c => EF.Functions.ILike(c.Name, escaped + "%") ? 0 : 1)
                .ThenByDescending(c => c.RecipeReferenceCount)
                .ThenBy(c => c.Name)
                .Take(limit);
        }

        // Fallback for the EF Core in-memory provider used in unit tests.
        // EF.Functions.ILike is Npgsql-only and will throw a NotSupportedException
        // when evaluated against any other provider.
        var lower = search.ToLowerInvariant();
        return source
            .Where(c => c.Name.ToLower().Contains(lower))
            .OrderBy(c => c.Name.ToLower().StartsWith(lower) ? 0 : 1)
            .ThenByDescending(c => c.RecipeReferenceCount)
            .ThenBy(c => c.Name)
            .Take(limit);
    }

    /// <summary>
    /// Escapes the ILike wildcard metacharacters (<c>%</c>, <c>_</c>, <c>\</c>) in
    /// <paramref name="term"/> so they are treated as literal characters in the pattern
    /// rather than as wildcards.
    /// </summary>
    internal static string EscapeILikeWildcards(string term) =>
        term.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";
}
