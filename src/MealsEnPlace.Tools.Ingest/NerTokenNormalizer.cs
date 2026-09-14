namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// Reason a NER token was rejected during normalization.
/// </summary>
internal enum NerTokenRejectionReason
{
    /// <summary>Token collapsed to an empty string after all cleanup rules ran.</summary>
    EmptyAfterCleanup,

    /// <summary>Token contained a URL scheme ("://"), indicating NER extraction captured a link rather than an ingredient.</summary>
    LooksLikeUrl,

    /// <summary>Token contained no alphabetic characters after cleanup.</summary>
    NoLetters,

    /// <summary>No rejection; the token was accepted and normalized successfully.</summary>
    None,

    /// <summary>Every remaining word in the token was in the stopword or connective list.</summary>
    StopwordsOnly
}

/// <summary>
/// The outcome of normalizing a single NER token.
/// <see cref="IsRejected"/> discriminates between the two outcomes; access
/// <see cref="NormalizedValue"/> only when <see cref="IsRejected"/> is false.
/// </summary>
internal readonly record struct NerTokenNormalizationResult
{
    /// <summary>True when the token must not produce a CanonicalIngredient row.</summary>
    public bool IsRejected { get; init; }

    /// <summary>
    /// The cleaned, normalized token value. Null when <see cref="IsRejected"/> is true.
    /// </summary>
    public string? NormalizedValue { get; init; }

    /// <summary>
    /// Describes why the token was rejected.
    /// <see cref="NerTokenRejectionReason.None"/> when the token was accepted.
    /// </summary>
    public NerTokenRejectionReason RejectionReason { get; init; }
}

/// <summary>
/// Normalizes a single NER token from the Kaggle dataset before it is passed to
/// <see cref="CanonicalIngredientRegistry.GetOrCreate"/>.
/// </summary>
/// <remarks>
/// Normalization rules, applied in order:
/// <list type="number">
///   <item>Trim leading and trailing whitespace.</item>
///   <item>Reject if the token contains a URL scheme ("://") -- a sign that NER extraction captured a recipe source link instead of an ingredient.</item>
///   <item>Strip leading and trailing characters that are not letters or digits (internal apostrophes and hyphens survive).</item>
///   <item>Collapse runs of internal whitespace to a single space.</item>
///   <item>Repeatedly strip the trailing word when it is in the stopword list.</item>
///   <item>Reject if the result is empty.</item>
///   <item>Reject if the result contains no alphabetic character.</item>
///   <item>Reject if every remaining word is in the stopword list.</item>
/// </list>
/// Casing is preserved throughout — the registry deduplicates case-insensitively.
/// </remarks>
internal static class NerTokenNormalizer
{
    private static string CollapseInternalWhitespace(string value)
    {
        // Fast path: no consecutive spaces means nothing to collapse.
        if (!value.Contains("  ", StringComparison.Ordinal))
        {
            return value;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    // Apostrophes are deliberately not edge characters: "'apple'" must become
    // "apple", while an internal apostrophe ("confectioners' sugar") is untouched
    // because only the edges are scanned.
    private static bool IsIngredientEdgeChar(char c) =>
        char.IsLetter(c) || char.IsDigit(c);

    /// <summary>
    /// Applies all normalization rules to <paramref name="rawToken"/> and returns
    /// either the cleaned value or a rejection reason. This method is a pure function:
    /// calling it twice with the same input always produces the same output, and
    /// calling it on an already-normalized value is idempotent.
    /// </summary>
    public static NerTokenNormalizationResult Normalize(string rawToken)
    {
        // Rule 1: trim leading and trailing whitespace.
        var working = rawToken.Trim();

        // Rule 2: reject tokens that look like a URL. The Kaggle NER column occasionally
        // captures an entire recipe source link (e.g. "http://www.foodnetwork.com/...")
        // instead of an ingredient phrase; "://" is a cheap, reliable signal for that.
        if (working.Contains("://", StringComparison.Ordinal))
        {
            return new NerTokenNormalizationResult
            {
                IsRejected = true,
                RejectionReason = NerTokenRejectionReason.LooksLikeUrl
            };
        }

        // Rule 3: strip leading and trailing characters that are not letters, digits, or apostrophes.
        working = StripEdgeNonIngredientChars(working);

        // Rule 4: collapse runs of internal whitespace to a single space.
        working = CollapseInternalWhitespace(working);

        // Rule 5: repeatedly strip the trailing word when it is a stopword.
        working = StripTrailingStopwords(working);

        // Rule 6: reject if empty.
        if (working.Length == 0)
        {
            return new NerTokenNormalizationResult
            {
                IsRejected = true,
                RejectionReason = NerTokenRejectionReason.EmptyAfterCleanup
            };
        }

        // Rule 7: reject if no alphabetic character remains.
        var hasLetter = false;
        foreach (var c in working)
        {
            if (char.IsLetter(c))
            {
                hasLetter = true;
                break;
            }
        }

        if (!hasLetter)
        {
            return new NerTokenNormalizationResult
            {
                IsRejected = true,
                RejectionReason = NerTokenRejectionReason.NoLetters
            };
        }

        // Rule 8: reject if every remaining word is a stopword.
        var words = working.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allStopwords = true;
        foreach (var word in words)
        {
            if (!Stopwords.Contains(word))
            {
                allStopwords = false;
                break;
            }
        }

        if (allStopwords)
        {
            return new NerTokenNormalizationResult
            {
                IsRejected = true,
                RejectionReason = NerTokenRejectionReason.StopwordsOnly
            };
        }

        return new NerTokenNormalizationResult
        {
            IsRejected = false,
            NormalizedValue = working,
            RejectionReason = NerTokenRejectionReason.None
        };
    }

    /// <summary>
    /// Stopword list used to detect trailing connectives and purely functional tokens.
    /// Ordered alphabetically.
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "add", "an", "and", "for", "of", "or", "plus", "the", "to", "with"
    };

    private static string StripEdgeNonIngredientChars(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var start = 0;
        while (start < value.Length && !IsIngredientEdgeChar(value[start]))
        {
            start++;
        }

        var end = value.Length - 1;
        while (end >= start && !IsIngredientEdgeChar(value[end]))
        {
            end--;
        }

        return start > end ? string.Empty : value[start..(end + 1)];
    }

    private static string StripTrailingStopwords(string value)
    {
        // Never strip the last remaining word: a token that is nothing but
        // stopwords must reach rule 7 and be rejected as StopwordsOnly, not
        // collapse to empty here and be misreported as EmptyAfterCleanup.
        while (true)
        {
            var lastSpaceIndex = value.LastIndexOf(' ');
            if (lastSpaceIndex < 0)
            {
                break;
            }

            var trailingWord = value[(lastSpaceIndex + 1)..];
            if (!Stopwords.Contains(trailingWord))
            {
                break;
            }

            value = value[..lastSpaceIndex];
        }

        return value;
    }
}
