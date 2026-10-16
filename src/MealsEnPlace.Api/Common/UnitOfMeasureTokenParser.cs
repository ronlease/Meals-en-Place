using System.Text.RegularExpressions;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// Splits a raw measure string into a numeric quantity and a unit token.
/// Handles common formats: "2 cups", "500g", "1/2 tbsp", "1 1/2 cups", "a knob".
/// <see cref="ParseWithIngredientContext"/> additionally derives ordered unit token
/// candidates and decides whether the remainder is a count followed by the ingredient
/// noun (e.g. "4 chicken breasts") rather than a unit.
/// <para>
/// MEP-067: also drops range upper bounds and sums hyphenated mixed numbers
/// ("1-1/2", "2-3"), skips junk words and size descriptors before unit detection,
/// flags junk and to-taste lines, and detects butter sticks.
/// </para>
/// Pure function with no dependencies; shared between the runtime service
/// (<see cref="UnitOfMeasureNormalizationService"/>) and the offline ingest tool so
/// both paths see identical parse semantics.
/// </summary>
public static partial class UnitOfMeasureTokenParser
{
    private static readonly HashSet<string> ButterContextExcludedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "almond",
        "apple",
        "cashew",
        "cocoa",
        "nut",
        "peanut",
        "shea",
        "sunflower"
    };

    private static readonly HashSet<string> ButterContextRequiredWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "butter",
        "margarine"
    };

    /// <summary>
    /// Grams in one US standard stick of butter or margarine (a quarter pound). A defined
    /// size, not a product-size assumption.
    /// </summary>
    public const decimal ButterStickGrams = 113m;

    private static readonly HashSet<string> CountNounLeadingDescriptors = new(StringComparer.OrdinalIgnoreCase)
    {
        "boneless",
        "extra",
        "extra-large",
        "jumbo",
        "large",
        "medium",
        "ripe",
        "skinless",
        "small",
        "whole"
    };

    /// <summary>Abbreviation of the seeded dash unit.</summary>
    public const string DashUnitOfMeasureAbbreviation = "dash";

    private static readonly HashSet<string> ImplicitQuantityLeadingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "dash",
        "dashes",
        "pinch",
        "pinches"
    };

    private static readonly HashSet<string> IndefiniteArticles = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an"
    };

    private static readonly HashSet<string> JunkLines = new(StringComparer.OrdinalIgnoreCase)
    {
        "-",
        "–",
        "none",
        "or",
        "to"
    };

    private static readonly HashSet<string> JunkWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "-",
        "–",
        "more",
        "none",
        "or",
        "to"
    };

    /// <summary>Abbreviation of the seeded pinch unit.</summary>
    public const string PinchUnitOfMeasureAbbreviation = "pinch";

    private static readonly HashSet<string> SeasoningOnlyLines = new(StringComparer.Ordinal)
    {
        "black pepper",
        "freshly ground black pepper",
        "ground black pepper",
        "kosher salt",
        "pepper",
        "pepper and salt",
        "salt",
        "salt & pepper",
        "salt and black pepper",
        "salt and freshly ground black pepper",
        "salt and pepper",
        "sea salt"
    };

    private static readonly HashSet<string> SizeDescriptors = new(StringComparer.OrdinalIgnoreCase)
    {
        "extra-large",
        "jumbo",
        "large",
        "lg",
        "lg.",
        "med",
        "med.",
        "medium",
        "small",
        "sm",
        "sm."
    };

    private static readonly char[] TrailingPunctuation = [',', ';', ':'];

    private static bool ContainsAnyWord(string text, HashSet<string> words) =>
        WordPattern().Matches(text).Any(m => words.Contains(m.Value));

    [GeneratedRegex(@"^-(\d+/\d+)(?![\d/.])")]
    private static partial Regex HyphenMixedNumberPattern();

    private static bool IsPepperContext(string context) =>
        WordPattern().Matches(context).Any(m => string.Equals(m.Value, "pepper", StringComparison.OrdinalIgnoreCase));

    private static bool IsSimpleFraction(string chunk)
    {
        var slash = chunk.IndexOf('/');
        if (slash <= 0 || slash == chunk.Length - 1)
        {
            return false;
        }

        return chunk[..slash].All(char.IsAsciiDigit) && chunk[(slash + 1)..].All(char.IsAsciiDigit);
    }

    private static string NormalizeLine(string line)
    {
        var lowered = line.Replace("(", " ").Replace(")", " ").ToLowerInvariant();
        var collapsed = string.Join(' ', lowered.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.TrimEnd('.').Trim();
    }

    /// <summary>
    /// Splits <paramref name="measureString"/> into a numeric quantity and
    /// the remainder as a unit token. Returns <c>(0, wholeString)</c> when
    /// no leading number is found so the caller can route to Claude or the
    /// review queue. Supports mixed numbers such as "1 1/2 cups" and "1-1/2 cups"
    /// (summed), and ranges such as "2-3 tbsp", "2 - 3 tsp" or "1 to 1 1/4 c."
    /// (lower bound kept, upper bound dropped).
    /// </summary>
    public static (decimal Quantity, string UnitToken) Parse(string measureString)
    {
        var trimmed = measureString.Trim();

        var numericEnd = 0;
        while (numericEnd < trimmed.Length
               && (char.IsDigit(trimmed[numericEnd])
                   || trimmed[numericEnd] == '.'
                   || trimmed[numericEnd] == '/'))
        {
            numericEnd++;
        }

        if (numericEnd == 0)
        {
            return (0m, trimmed);
        }

        var numericPart = trimmed[..numericEnd];
        var remainder = trimmed[numericEnd..].Trim();

        var quantity = ParseFractionOrDecimal(numericPart);
        var wasMixed = false;

        // Hyphen mixed number: "1-1/2 cups" => 1.5 with remainder "cups".
        if (quantity > 0m && numericPart.All(char.IsAsciiDigit))
        {
            var hyphenMatch = HyphenMixedNumberPattern().Match(remainder);
            if (hyphenMatch.Success)
            {
                var fraction = ParseFractionOrDecimal(hyphenMatch.Groups[1].Value);
                if (fraction > 0m)
                {
                    quantity += fraction;
                    remainder = remainder[hyphenMatch.Length..].Trim();
                    wasMixed = true;
                }
            }
        }

        // Mixed number: "1 1/2 cups" => 1.5 with remainder "cups".
        if (!wasMixed && !numericPart.Contains('/') && quantity > 0m)
        {
            var chunkEnd = remainder.IndexOfAny([' ', '\t', '-', '–']);
            var chunk = chunkEnd < 0 ? remainder : remainder[..chunkEnd];
            if (IsSimpleFraction(chunk))
            {
                var fraction = ParseFractionOrDecimal(chunk);
                if (fraction > 0m)
                {
                    quantity += fraction;
                    remainder = chunkEnd < 0 ? string.Empty : remainder[chunkEnd..].Trim();
                }
            }
        }

        // Range: "2-3 tbsp", "2 - 3 tsp", "1 to 1 1/4 c." => keep the lower bound only.
        if (quantity > 0m)
        {
            var rangeMatch = RangeUpperBoundPattern().Match(remainder);
            if (rangeMatch.Success)
            {
                remainder = remainder[rangeMatch.Length..].Trim();
            }
        }

        return (quantity, remainder);
    }

    /// <summary>
    /// Parses a numeric string that may be a simple decimal ("1.5") or a
    /// fraction ("1/2"). Returns 0 on parse failure.
    /// </summary>
    private static decimal ParseFractionOrDecimal(string numericPart)
    {
        if (numericPart.Contains('/'))
        {
            var parts = numericPart.Split('/');
            if (parts.Length == 2
                && decimal.TryParse(parts[0], out var numerator)
                && decimal.TryParse(parts[1], out var denominator)
                && denominator != 0)
            {
                return numerator / denominator;
            }
            return 0m;
        }

        return decimal.TryParse(numericPart, out var value) ? value : 0m;
    }

    /// <summary>
    /// Parses <paramref name="measureString"/> and derives the ordered unit token
    /// candidates, the review-queue deferral token, junk / to-taste flags, butter-stick
    /// weight, and whether the remainder is a count followed by <paramref name="ingredientName"/>.
    /// Junk words and size descriptors are skipped before candidate detection.
    /// </summary>
    public static MeasureStringParseResult ParseWithIngredientContext(
        string measureString,
        string? ingredientName)
    {
        var (quantity, remainder) = Parse(measureString);

        if (quantity <= 0m)
        {
            var junkLine = JunkLines.Contains(remainder.Trim().TrimEnd('.'));
            var normalizedLine = NormalizeLine(remainder);
            var toTaste = !junkLine
                          && (ToTastePattern().IsMatch(normalizedLine) || SeasoningOnlyLines.Contains(normalizedLine));
            var suppressed = junkLine || toTaste;
            var implicitUnit = suppressed ? null : ResolveImplicitUnitAbbreviation(remainder, ingredientName);

            return new MeasureStringParseResult
            {
                DeferralUnitToken = suppressed ? string.Empty : remainder,
                ImplicitQuantity = implicitUnit is null ? null : 1m,
                IsCountWithIngredientNoun = false,
                IsJunkLine = junkLine,
                IsToTaste = toTaste,
                PreferredUnitOfMeasureAbbreviation = implicitUnit,
                Quantity = quantity,
                UnitTokenCandidates = suppressed || string.IsNullOrWhiteSpace(remainder) ? [] : [remainder]
            };
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            return new MeasureStringParseResult
            {
                DeferralUnitToken = string.Empty,
                IsCountWithIngredientNoun = true,
                Quantity = quantity,
                UnitTokenCandidates = []
            };
        }

        var allWords = StripLeadingParenthetical(remainder)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var startsWithSizeDescriptor = false;
        var skipped = 0;
        while (skipped < allWords.Length)
        {
            var word = allWords[skipped].TrimEnd(TrailingPunctuation);
            if (SizeDescriptors.Contains(word))
            {
                startsWithSizeDescriptor = true;
            }
            else if (!JunkWords.Contains(word))
            {
                break;
            }

            skipped++;
        }

        var words = allWords[skipped..];
        if (words.Length == 0)
        {
            return new MeasureStringParseResult
            {
                DeferralUnitToken = string.Empty,
                IsCountWithIngredientNoun = true,
                Quantity = quantity,
                StartsWithSizeDescriptor = startsWithSizeDescriptor,
                UnitTokenCandidates = []
            };
        }

        var firstWord = words[0];
        var firstWordTrimmed = firstWord.TrimEnd(TrailingPunctuation);

        var candidates = new List<string>();
        if (words.Length >= 2)
        {
            candidates.Add($"{words[0]} {words[1]}");
        }

        candidates.Add(firstWord);
        candidates.Add(firstWordTrimmed);

        return new MeasureStringParseResult
        {
            DeferralUnitToken = firstWordTrimmed,
            IngredientSpecificGramsPerUnit = ResolveButterStickGrams(words, ingredientName),
            IsCountWithIngredientNoun = StartsWithIngredientNoun(remainder, ingredientName),
            Quantity = quantity,
            StartsWithSizeDescriptor = startsWithSizeDescriptor,
            UnitTokenCandidates = candidates
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList()
        };
    }

    [GeneratedRegex(@"^(?:[-–]|(?:to|or)(?=\s))\s*(?:\d+\.\d+|\.\d+|\d+/\d+|\d+)(?:\s+\d+/\d+)?", RegexOptions.IgnoreCase)]
    private static partial Regex RangeUpperBoundPattern();

    private static decimal? ResolveButterStickGrams(string[] words, string? ingredientName)
    {
        var unitWord = words[0].TrimEnd('.', ',', ';', ':').ToLowerInvariant();
        if (unitWord is not ("stick" or "sticks"))
        {
            return null;
        }

        var context = string.IsNullOrWhiteSpace(ingredientName)
            ? string.Join(' ', words.Skip(1))
            : ingredientName;

        return ContainsAnyWord(context, ButterContextRequiredWords)
               && !ContainsAnyWord(context, ButterContextExcludedWords)
            ? ButterStickGrams
            : null;
    }

    /// <summary>
    /// For a quantity-less line that leads with pinch / pinches / dash / dashes (optionally
    /// after "a" or "an"), returns the unit abbreviation to use with an implied quantity of 1:
    /// "dash" when the ingredient context contains the whole word "pepper", otherwise "pinch".
    /// Returns null when the line does not lead with one of those words.
    /// </summary>
    private static string? ResolveImplicitUnitAbbreviation(string remainder, string? ingredientName)
    {
        var words = remainder.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var index = words.Length > 0 && IndefiniteArticles.Contains(words[0]) ? 1 : 0;
        if (index >= words.Length || !ImplicitQuantityLeadingWords.Contains(words[index].TrimEnd(TrailingPunctuation)))
        {
            return null;
        }

        var rest = words.Skip(index + 1).ToList();
        string context;
        if (!string.IsNullOrWhiteSpace(ingredientName))
        {
            context = ingredientName;
        }
        else
        {
            var ofIndex = rest.FindIndex(w => string.Equals(w, "of", StringComparison.OrdinalIgnoreCase));
            context = string.Join(' ', ofIndex >= 0 ? rest.Skip(ofIndex + 1) : rest);
        }

        return IsPepperContext(context) ? DashUnitOfMeasureAbbreviation : PinchUnitOfMeasureAbbreviation;
    }

    private static bool SingularOrPluralMatch(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(left + "s", right, StringComparison.OrdinalIgnoreCase)
               || string.Equals(left + "es", right, StringComparison.OrdinalIgnoreCase)
               || string.Equals(left, right + "s", StringComparison.OrdinalIgnoreCase)
               || string.Equals(left, right + "es", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithIngredientNoun(string remainder, string? ingredientName)
    {
        if (string.IsNullOrWhiteSpace(ingredientName))
        {
            return false;
        }

        var ingredientWords = ingredientName.Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (ingredientWords.Length == 0)
        {
            return false;
        }

        var ingredientFirstWord = ingredientWords[0].TrimEnd(TrailingPunctuation);
        var words = StripLeadingParenthetical(remainder.ToLowerInvariant())
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var index = 0;
        while (index < words.Length
               && (CountNounLeadingDescriptors.Contains(words[index].TrimEnd(TrailingPunctuation))
                   || JunkWords.Contains(words[index].TrimEnd(TrailingPunctuation))))
        {
            index++;
        }

        if (index >= words.Length)
        {
            return false;
        }

        return SingularOrPluralMatch(words[index].TrimEnd(TrailingPunctuation), ingredientFirstWord);
    }

    private static string StripLeadingParenthetical(string value)
    {
        var trimmed = value.TrimStart();
        if (!trimmed.StartsWith('('))
        {
            return trimmed;
        }

        var close = trimmed.IndexOf(')');
        return close < 0 ? trimmed : trimmed[(close + 1)..].TrimStart();
    }

    [GeneratedRegex(@"\bto taste\b")]
    private static partial Regex ToTastePattern();

    [GeneratedRegex(@"[a-z]+", RegexOptions.IgnoreCase)]
    private static partial Regex WordPattern();
}
