using System.Text.RegularExpressions;

namespace MealsEnPlace.Tools.Dedup;

/// <summary>
/// Pure string normalizer that produces a fold-group key for a
/// <see cref="MealsEnPlace.Api.Models.Entities.CanonicalIngredient"/> name.
/// Two canonical names that normalize to the same key are considered
/// morphological variants of the same ingredient and become fold candidates.
/// <para>
/// Rules, applied in order:
/// <list type="bullet">
///   <item><description>Lowercase the input.</description></item>
///   <item><description>Apply a curated typo/synonym phrase dictionary (<see cref="TypoAndSynonymPhraseReplacements"/>) so misspellings and compound/split-word variants (e.g. "chickpea" vs. "chick pea") share a key with their correctly-spelled counterpart. Deliberately NOT fuzzy/edit-distance matching -- "pea" and "pear" are one edit apart, and automatic distance-based folding could silently corrupt recipe matching data.</description></item>
///   <item><description>Strip known brand-name phrases (<see cref="BrandPhraseReplacements"/>) entirely -- brand is not part of an ingredient's identity. Matched as whole phrases, not individual-word stopwords, because a brand can share a word with a real, substance-changing descriptor (the "Green Giant" brand vs. a plain green pepper/pea/onion).</description></item>
///   <item><description>Split on whitespace, comma, parentheses, hyphen, and forward slash.</description></item>
///   <item><description>Remove cosmetic prep-cut words (<see cref="PrepCutStopwords"/>) and filler/quantity/authoring-artifact words (<see cref="FillerStopwords"/>). Preservation-state words (<c>fresh</c>, <c>frozen</c>, <c>dried</c>, <c>cooked</c>, <c>raw</c>, <c>uncooked</c>) and size-semantic modifiers (<c>baby</c>, <c>mini</c>, <c>jumbo</c>) are deliberately NOT stopwords -- they change the substance of the ingredient, so "frozen pea" stays distinct from "fresh pea" the same way "baby carrot" stays distinct from "carrot".</description></item>
///   <item><description>Collapse plural endings: <c>-ies</c> to <c>-y</c>, <c>-es</c> to <c>-e</c>-or-drop, <c>-s</c> to removed. Conservative: word-by-word, only if the resulting token is at least two characters.</description></item>
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
    /// Cosmetic prep-cut modifiers that do not change which ingredient is
    /// being referenced. A recipe calling for "chopped onion" can be fulfilled
    /// by the user's "onion" and vice versa, so these words drop at normalize
    /// time.
    /// <para>
    /// Preservation-state words (<c>fresh</c>, <c>frozen</c>, <c>dried</c>,
    /// <c>cooked</c>, <c>raw</c>, <c>uncooked</c>), size-semantic modifiers
    /// (<c>baby</c>, <c>mini</c>, <c>jumbo</c>), and cure/process modifiers
    /// (<c>smoked</c>, <c>pickled</c>, <c>candied</c>) are intentionally NOT
    /// in this list because they change the substance of the ingredient.
    /// Preservation state in particular affects shelf life and seasonality
    /// matching, so "frozen peas" and "fresh peas" must stay distinct
    /// canonicals (MEP-050).
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> PrepCutStopwords = new HashSet<string>(StringComparer.Ordinal)
    {
        "chopped",
        "crushed",
        "cubed",
        "cut",
        "diced",
        "grated",
        "ground",
        "halved",
        "minced",
        "peeled",
        "quartered",
        "seeded",
        "shredded",
        "sliced",
        "trimmed",
        "whole"
    };

    /// <summary>
    /// Filler, quantity, and recipe-authoring-artifact words that carry no
    /// ingredient identity -- "a handful of peas" and "peas" are the same
    /// ingredient. Kept separate from <see cref="PrepCutStopwords"/> so the
    /// reason each word is cosmetic stays documented at the point of use.
    /// </summary>
    public static readonly IReadOnlySet<string> FillerStopwords = new HashSet<string>(StringComparer.Ordinal)
    {
        "and",
        "bag",
        "bags",
        "choice",
        "either",
        "etc",
        "gallon",
        "gallons",
        "handful",
        "handfuls",
        "if",
        "kilogram",
        "kilograms",
        "mugful",
        "mugfuls",
        "of",
        "optional",
        "or",
        "packet",
        "packets",
        "pint",
        "pints"
    };

    // DefaultStopwords depends on PrepCutStopwords and FillerStopwords, so it
    // must be declared after both: C# runs static field initializers in
    // textual order, and an out-of-order reference would see an
    // uninitialized (null) field here.

    /// <summary>
    /// Union of <see cref="PrepCutStopwords"/> and <see cref="FillerStopwords"/> --
    /// the full set of single-token words this normalizer treats as cosmetic noise.
    /// </summary>
    public static readonly IReadOnlySet<string> DefaultStopwords =
        new HashSet<string>(PrepCutStopwords.Concat(FillerStopwords), StringComparer.Ordinal);

    /// <summary>Default normalizer instance using <see cref="DefaultStopwords"/>.</summary>
    public static readonly CanonicalNameNormalizer Default = new(DefaultStopwords);

    /// <summary>
    /// Known brand names that do not change which ingredient is being
    /// referenced (e.g. "lesueur peas" and "peas" are the same ingredient).
    /// Matched and removed as whole phrases -- not individual-word stopwords --
    /// because a brand can share a word with a real, substance-changing
    /// descriptor (the "Green Giant" brand vs. plain "green" bell pepper/pea/onion).
    /// This list is expected to grow as more brands surface across the wider
    /// catalog, not just peas.
    /// </summary>
    private static readonly (Regex Pattern, string Replacement)[] BrandPhraseReplacements =
    [
        (BuildWholeWordPattern("birds eye"), string.Empty),
        (BuildWholeWordPattern("campbell's"), string.Empty),
        (BuildWholeWordPattern("del monte"), string.Empty),
        (BuildWholeWordPattern("green giant"), string.Empty),
        (BuildWholeWordPattern("knorr"), string.Empty),
        (BuildWholeWordPattern("lesueur"), string.Empty)
    ];

    /// <summary>
    /// Hand-curated typo and compound/split-word synonym corrections, applied
    /// as whole-word phrase replacements before tokenization. Deliberately NOT
    /// fuzzy/edit-distance matching: automatic distance-based folding is
    /// dangerous in this domain ("pea" and "pear" are one edit apart) and
    /// could silently corrupt recipe matching data. Every entry here was
    /// found and manually verified during the MEP-050 investigation.
    /// Applied before <see cref="BrandPhraseReplacements"/> so a corrected
    /// spelling (e.g. "lesuer" to "lesueur") is still eligible for brand
    /// stripping.
    /// </summary>
    private static readonly (Regex Pattern, string Replacement)[] TypoAndSynonymPhraseReplacements =
    [
        (BuildWholeWordPattern("back eyed"), "black eyed"),
        (BuildWholeWordPattern("black eye"), "black eyed"),
        (BuildWholeWordPattern("blacck eyed"), "black eyed"),
        (BuildWholeWordPattern("blackeyed"), "black eyed"),
        (BuildWholeWordPattern("chickpeas"), "chick peas"),
        (BuildWholeWordPattern("chickpea"), "chick pea"),
        // "e.g." keeps its trailing period out of the trailing \b assertion:
        // \b needs a word/non-word transition, and two punctuation characters
        // in a row (". ") never produce one.
        (new Regex(@"\be\.g\b\.?", RegexOptions.Compiled | RegexOptions.CultureInvariant), string.Empty),
        (BuildWholeWordPattern("earlie"), "early"),
        (BuildWholeWordPattern("frozed"), "frozen"),
        (BuildWholeWordPattern("leseur"), "lesueur"),
        (BuildWholeWordPattern("lesueuer"), "lesueur"),
        (BuildWholeWordPattern("lesuer"), "lesueur"),
        (BuildWholeWordPattern("pidgeaon"), "pigeon"),
        (BuildWholeWordPattern("slit"), "split"),
        (BuildWholeWordPattern("sping"), "spring"),
        (BuildWholeWordPattern("spit"), "split"),
        (BuildWholeWordPattern("splitt"), "split")
    ];

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

        var working = name.ToLowerInvariant();
        working = ApplyPhraseReplacements(working, TypoAndSynonymPhraseReplacements);
        working = ApplyPhraseReplacements(working, BrandPhraseReplacements);

        var tokens = working
            .Split([' ', '\t', ',', '(', ')', '-', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !_stopwords.Contains(t))
            .Select(Singularize)
            .Where(t => t.Length > 0)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();

        return string.Join(' ', tokens);
    }

    private static string ApplyPhraseReplacements(string value, IReadOnlyList<(Regex Pattern, string Replacement)> replacements)
    {
        foreach (var (pattern, replacement) in replacements)
        {
            value = pattern.Replace(value, replacement);
        }

        return value;
    }

    private static Regex BuildWholeWordPattern(string phrase) =>
        new($@"\b{phrase}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
