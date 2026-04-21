namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// Pure string normalizer that produces a fold-group key for a
/// <see cref="MealsEnPlace.Api.Models.Entities.CanonicalIngredient"/> name.
/// Two canonical names that normalize to the same key are considered
/// morphological variants of the same ingredient and become fold candidates.
/// <para>
/// Rules:
/// <list type="bullet">
///   <item><description>Lowercase and whitespace-collapse the input.</description></item>
///   <item><description>Remove cosmetic prep and state modifier words (<c>chopped</c>, <c>diced</c>, <c>fresh</c>, etc.). The stopword list is deliberately narrow: modifiers that change the substance of the ingredient (<c>baby</c>, <c>mini</c>, <c>jumbo</c>, <c>smoked</c>, <c>dry</c>-roasted etc.) are NOT stopwords so "baby carrot" stays distinct from "carrot".</description></item>
///   <item><description>Collapse plural endings: <c>-ies</c> → <c>-y</c>, <c>-es</c> → <c>-e</c>-or-drop, <c>-s</c> → remove. Conservative: word-by-word, only if the resulting token is at least two characters.</description></item>
///   <item><description>Sort the remaining words. "black pepper" and "pepper black" both normalize to "black pepper" so token-order noise folds.</description></item>
/// </list>
/// </para>
/// <para>
/// Instance is immutable; the singleton <see cref="Default"/> is thread-safe
/// for the simple read-only case. Callers that want a different stopword
/// list build a new instance.
/// </para>
/// </summary>
internal sealed class CanonicalNameNormalizer
{
    /// <summary>
    /// Cosmetic / prep / state modifiers that do not change which ingredient is
    /// being referenced. A recipe calling for "chopped onion" can be fulfilled
    /// by the user's "onion" and vice versa, so these words drop at normalize
    /// time.
    /// <para>
    /// Size-semantic modifiers (<c>baby</c>, <c>mini</c>, <c>jumbo</c>) and
    /// cure/process modifiers (<c>smoked</c>, <c>pickled</c>, <c>candied</c>)
    /// are intentionally NOT in this list because they change the substance
    /// of the ingredient.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> DefaultStopwords = new HashSet<string>(StringComparer.Ordinal)
    {
        "chopped",
        "cooked",
        "crushed",
        "cubed",
        "cut",
        "diced",
        "dried",
        "fresh",
        "frozen",
        "grated",
        "ground",
        "halved",
        "minced",
        "peeled",
        "quartered",
        "raw",
        "seeded",
        "shredded",
        "sliced",
        "trimmed",
        "uncooked",
        "whole"
    };

    /// <summary>Default normalizer instance using <see cref="DefaultStopwords"/>.</summary>
    public static readonly CanonicalNameNormalizer Default = new(DefaultStopwords);

    private readonly IReadOnlySet<string> _stopwords;

    public CanonicalNameNormalizer(IReadOnlySet<string> stopwords)
    {
        _stopwords = stopwords;
    }

    /// <summary>
    /// Normalizes <paramref name="name"/> to a fold-group key. Returns an
    /// empty string if the name is null, whitespace, or reduces to nothing
    /// after stopword removal (which should not happen for any real canonical
    /// ingredient, but keeps the method total).
    /// </summary>
    public string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var tokens = name.ToLowerInvariant()
            .Split([' ', '\t', ',', '(', ')', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !_stopwords.Contains(t))
            .Select(Singularize)
            .Where(t => t.Length > 0)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();

        return string.Join(' ', tokens);
    }

    /// <summary>
    /// Conservative English singularization: handles the three common English
    /// plural shapes without pulling in a full morphology library. Skipped
    /// when the input is too short to safely strip (e.g., "is", "as") since
    /// those are almost never plurals.
    /// </summary>
    private static string Singularize(string token)
    {
        if (token.Length < 3)
        {
            return token;
        }

        // -ies → -y  (berries → berry, pastries → pastry)
        if (token.EndsWith("ies", StringComparison.Ordinal))
        {
            return token[..^3] + "y";
        }

        // -ches / -shes / -sses / -xes → drop -es  (tomatoes → tomato is
        // handled by the -es drop below; this is the narrower case for
        // consonant+h / double-s / x+es plurals that would otherwise lose
        // a meaningful character)
        if (token.EndsWith("ches", StringComparison.Ordinal)
            || token.EndsWith("shes", StringComparison.Ordinal)
            || token.EndsWith("sses", StringComparison.Ordinal)
            || token.EndsWith("xes", StringComparison.Ordinal))
        {
            return token[..^2];
        }

        // -oes → -o  (tomatoes → tomato, potatoes → potato)
        if (token.EndsWith("oes", StringComparison.Ordinal))
        {
            return token[..^2];
        }

        // -s → drop.  Skip double-s endings we already handled above.
        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.Ordinal))
        {
            return token[..^1];
        }

        return token;
    }
}
