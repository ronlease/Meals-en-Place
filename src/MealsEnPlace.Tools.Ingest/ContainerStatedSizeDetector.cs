using System.Text.RegularExpressions;
using MealsEnPlace.Api.Common;

namespace MealsEnPlace.Tools.Ingest;

/// <summary>
/// MEP-067 diagnostic. Reports whether a raw ingredient line that names a container
/// ("can", "pkg.", "jar"...) also states that container's net size in the recipe text,
/// for example "1 (14.5 oz) can diced tomatoes", "1 8-oz. pkg cream cheese" or
/// "1 can (15 oz.) beans". Pure function; measures only and never changes resolution.
/// <para>
/// Conservative by design: the size (number plus a weight or volume unit) must sit directly
/// next to the container word, either immediately before it (separated only by spaces,
/// periods, hyphens or a closing parenthesis) or immediately after it inside parentheses.
/// A number and unit elsewhere in the line ("1 can tomatoes, cut into 1 inch pieces",
/// "1 can tomatoes, 2 cups chopped") does not count. Ranges ("10 to 12 oz") count as
/// stated and report the lower bound.
/// </para>
/// </summary>
internal static partial class ContainerStatedSizeDetector
{
    // Mirrors ContainerReferenceDetector.ContainerKeywords (a const is required by the generated regexes).
    private const string ContainerWordAlternation = "bag|bottle|box|can|carton|container|envelope|jar|package|packet|pkg|pouch|tube";

    private const string ContainerWordGroup = "(?:" + ContainerWordAlternation + ")(?:e?s)?(?![a-z])";

    private const string NumberPattern = @"(?:\d+(?:\.\d+)?(?:\s+\d+/\d+)?|\d+/\d+|\.\d+)";

    private const string SizePattern =
        @"(?<![\d./])(?<lower>" + NumberPattern + @")(?:\s*(?:-|–|to)\s*" + NumberPattern + @")?[\s-]*(?<unit>"
        + UnitPattern + @")(?![a-z])(?:\.?\s+each(?![a-z]))?";

    private static readonly Dictionary<string, string> UnitAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cup"] = "cup",
        ["cups"] = "cup",
        ["fluid ounce"] = "fl oz",
        ["fluid ounces"] = "fl oz",
        ["gal"] = "gal",
        ["gallon"] = "gal",
        ["gallons"] = "gal",
        ["g"] = "g",
        ["gram"] = "g",
        ["grams"] = "g",
        ["kg"] = "kg",
        ["kilogram"] = "kg",
        ["kilograms"] = "kg",
        ["l"] = "l",
        ["lb"] = "lb",
        ["lbs"] = "lb",
        ["liter"] = "l",
        ["liters"] = "l",
        ["litre"] = "l",
        ["litres"] = "l",
        ["milliliter"] = "ml",
        ["milliliters"] = "ml",
        ["millilitre"] = "ml",
        ["millilitres"] = "ml",
        ["ml"] = "ml",
        ["ounce"] = "oz",
        ["ounces"] = "oz",
        ["oz"] = "oz",
        ["pint"] = "pt",
        ["pints"] = "pt",
        ["pound"] = "lb",
        ["pounds"] = "lb",
        ["pt"] = "pt",
        ["qt"] = "qt",
        ["quart"] = "qt",
        ["quarts"] = "qt"
    };

    private const string UnitPattern =
        @"fl\.?\s*oz|fluid\s+ounces?|ounces?|oz|pounds?|lbs?|kilograms?|kg|grams?|g|milliliters?|millilitres?|ml|liters?|litres?|l|cups?|pints?|pt|quarts?|qt|gallons?|gal";

    /// <summary>
    /// Evaluates <paramref name="rawIngredient"/>. Returns a not-stated result for null, blank,
    /// or lines with no adjacent container word and size.
    /// </summary>
    public static ContainerStatedSizeResult Detect(string? rawIngredient)
    {
        if (string.IsNullOrWhiteSpace(rawIngredient))
        {
            return ContainerStatedSizeResult.NotStated;
        }

        var match = SizeBeforeContainerPattern().Match(rawIngredient);
        if (!match.Success)
        {
            match = SizeAfterContainerPattern().Match(rawIngredient);
        }

        if (!match.Success)
        {
            return ContainerStatedSizeResult.NotStated;
        }

        var (quantity, _) = UnitOfMeasureTokenParser.Parse(match.Groups["lower"].Value);
        var unit = Regex.Replace(match.Groups["unit"].Value.Trim().TrimEnd('.').ToLowerInvariant(), @"\s+", " ");
        if (unit.StartsWith("fl", StringComparison.Ordinal))
        {
            unit = "fl oz";
        }

        var isRange = match.Value.Contains(" to ", StringComparison.OrdinalIgnoreCase)
                      || Regex.IsMatch(match.Value, @"\d\s*[-–]\s*\d");

        return new ContainerStatedSizeResult
        {
            HasStatedSize = true,
            IsRange = isRange,
            Quantity = quantity > 0m ? quantity : null,
            UnitAbbreviation = UnitAbbreviations.GetValueOrDefault(unit, unit)
        };
    }

    [GeneratedRegex(@"(?<![a-z])" + ContainerWordGroup + @"[\s.]*\(\s*" + SizePattern, RegexOptions.IgnoreCase)]
    private static partial Regex SizeAfterContainerPattern();

    [GeneratedRegex(SizePattern + @"[\s.)-]*" + ContainerWordGroup, RegexOptions.IgnoreCase)]
    private static partial Regex SizeBeforeContainerPattern();
}

/// <summary>Outcome of <see cref="ContainerStatedSizeDetector.Detect"/>.</summary>
internal sealed record ContainerStatedSizeResult
{
    /// <summary>The shared negative result.</summary>
    public static readonly ContainerStatedSizeResult NotStated = new();

    /// <summary>True when a size sits next to the container word.</summary>
    public bool HasStatedSize { get; init; }

    /// <summary>True when the stated size was a range such as "10 to 12 oz" (lower bound reported).</summary>
    public bool IsRange { get; init; }

    /// <summary>Parsed quantity (lower bound for ranges), or null when not cleanly parseable.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>Normalized unit abbreviation (oz, fl oz, lb, g, kg, ml, l, cup, pt, qt, gal), or null.</summary>
    public string? UnitAbbreviation { get; init; }
}
