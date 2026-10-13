namespace MealsEnPlace.Api.Common;

/// <summary>
/// Splits a raw measure string into a numeric quantity and a unit token.
/// Handles common formats: "2 cups", "500g", "1/2 tbsp", "1 1/2 cups", "a knob".
/// <see cref="ParseWithIngredientContext"/> additionally derives ordered unit token
/// candidates and decides whether the remainder is a count followed by the ingredient
/// noun (e.g. "4 chicken breasts") rather than a unit.
/// Pure function with no dependencies; shared between the runtime service
/// (<see cref="UnitOfMeasureNormalizationService"/>) and the offline ingest tool so
/// both paths see identical parse semantics.
/// </summary>
public static class UnitOfMeasureTokenParser
{
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

    private static readonly char[] TrailingPunctuation = [',', ';', ':'];

    private static bool IsSimpleFraction(string chunk)
    {
        var slash = chunk.IndexOf('/');
        if (slash <= 0 || slash == chunk.Length - 1)
        {
            return false;
        }

        return chunk[..slash].All(char.IsAsciiDigit) && chunk[(slash + 1)..].All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Splits <paramref name="measureString"/> into a numeric quantity and
    /// the remainder as a unit token. Returns <c>(0, wholeString)</c> when
    /// no leading number is found so the caller can route to Claude or the
    /// review queue. Supports mixed numbers such as "1 1/2 cups".
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

        // Mixed number: "1 1/2 cups" => 1.5 with remainder "cups".
        if (!numericPart.Contains('/') && quantity > 0m)
        {
            var chunkEnd = remainder.IndexOfAny([' ', '\t']);
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
    /// candidates, the review-queue deferral token, and whether the remainder is a
    /// count followed by <paramref name="ingredientName"/>.
    /// </summary>
    public static MeasureStringParseResult ParseWithIngredientContext(
        string measureString,
        string? ingredientName)
    {
        var (quantity, remainder) = Parse(measureString);

        if (quantity <= 0m)
        {
            return new MeasureStringParseResult
            {
                DeferralUnitToken = remainder,
                IsCountWithIngredientNoun = false,
                Quantity = quantity,
                UnitTokenCandidates = string.IsNullOrWhiteSpace(remainder) ? [] : [remainder]
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

        var words = StripLeadingParenthetical(remainder)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var firstWord = words.Length > 0 ? words[0] : string.Empty;
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
            IsCountWithIngredientNoun = StartsWithIngredientNoun(remainder, ingredientName),
            Quantity = quantity,
            UnitTokenCandidates = candidates
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList()
        };
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
        while (index < words.Length && CountNounLeadingDescriptors.Contains(words[index].TrimEnd(TrailingPunctuation)))
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
}
