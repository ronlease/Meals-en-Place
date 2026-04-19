namespace MealsEnPlace.Api.Common;

/// <summary>
/// Detects container references in ingredient quantity strings.
/// A container reference names a packaging unit rather than a unit of measure
/// (e.g., "1 can", "1 jar", "1 box"). Container references must be flagged before
/// unit of measure parsing runs — the system never assumes a container size.
/// <para>
/// The keyword list is the primary detection mechanism. Claude is a fallback
/// for ambiguous cases that must be handled at the call site.
/// </para>
/// </summary>
public static class ContainerReferenceDetector
{
    /// <summary>
    /// The canonical set of container keywords checked against input strings.
    /// Detection is case-insensitive and matches whole words.
    /// </summary>
    public static readonly IReadOnlyList<string> ContainerKeywords = new[]
    {
        "bag",
        "bottle",
        "box",
        "can",
        "carton",
        "jar",
        "packet",
        "tube"
    };

    /// <summary>
    /// Scans <paramref name="input"/> for any container keyword.
    /// Detection is case-insensitive and uses whole-word matching so that
    /// ingredient names like "pecan" do not trigger a false positive on "can".
    /// </summary>
    /// <param name="input">
    /// The raw ingredient or measure string to evaluate
    /// (e.g., "1 can chopped tomatoes", "2 jars marinara"). May be null;
    /// null inputs return a negative result with an empty OriginalInput.
    /// </param>
    /// <returns>
    /// A <see cref="ContainerReferenceDetectionResult"/> describing whether
    /// a container reference was found, and which keyword matched.
    /// </returns>
    public static ContainerReferenceDetectionResult Detect(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ContainerReferenceDetectionResult.None(input ?? string.Empty);
        }

        foreach (var keyword in ContainerKeywords)
        {
            if (ContainsWholeWord(input, keyword) || ContainsWholeWord(input, keyword + "s"))
            {
                return ContainerReferenceDetectionResult.Detected(input, keyword);
            }
        }

        return ContainerReferenceDetectionResult.None(input);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool ContainsWholeWord(string input, string word)
    {
        var index = input.IndexOf(word, StringComparison.OrdinalIgnoreCase);

        while (index >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetter(input[index - 1]);
            var afterOk = index + word.Length == input.Length
                           || !char.IsLetter(input[index + word.Length]);

            if (beforeOk && afterOk)
            {
                return true;
            }

            index = input.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
