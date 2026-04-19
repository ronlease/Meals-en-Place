namespace MealsEnPlace.Api.Common;

/// <summary>
/// Splits a raw measure string into a numeric quantity and a unit token.
/// Handles common formats: "2 cups", "500g", "1/2 tbsp", "a knob".
/// Pure function with no dependencies; shared between the runtime service
/// (<see cref="UnitOfMeasureNormalizationService"/>) and the offline ingest tool so
/// both paths see identical parse semantics.
/// </summary>
public static class UnitOfMeasureTokenParser
{
    /// <summary>
    /// Splits <paramref name="measureString"/> into a numeric quantity and
    /// the remainder as a unit token. Returns <c>(0, wholeString)</c> when
    /// no leading number is found so the caller can route to Claude or the
    /// review queue.
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
}
