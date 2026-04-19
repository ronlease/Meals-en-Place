using System.Text.RegularExpressions;

namespace MealsEnPlace.Api.Common;

/// <summary>
/// Filters recipe instruction steps to drop blog-voice narrative while
/// retaining functional imperatives. Intended for use by the MEP-026 bulk
/// ingest pipeline when processing third-party recipe data; not applied to
/// user-entered recipes (MEP-018), where preserving user intent matters.
/// <para>
/// Filter rules (a step is dropped if any apply):
/// <list type="bullet">
///   <item><description>The step is empty or whitespace-only.</description></item>
///   <item><description>The step contains a first-person pronoun (I, me, my, we, our, us, mine), indicating blog narrative.</description></item>
///   <item><description>After stripping long parentheticals, the step exceeds <see cref="MaxWordsPerStep"/> words.</description></item>
/// </list>
/// </para>
/// <para>
/// Notably, this filter does NOT require the first word of a step to be a
/// recognized imperative verb. Legitimate instructions often start with a
/// preposition ("In a bowl, combine...") or a subordinating conjunction
/// ("When the mixture bubbles, stir...") -- the original prototype filter
/// over-dropped those cases. See MEP-026 findings and recommendation.
/// </para>
/// </summary>
public static partial class InstructionProseFilter
{
    /// <summary>
    /// Maximum word count retained per step, measured after long parentheticals
    /// are stripped. Steps longer than this are almost always narrative drift
    /// or merged multi-step blocks.
    /// </summary>
    public const int MaxWordsPerStep = 40;

    private static readonly char[] WordSplitSeparators = [' ', '\t', '\n', '\r'];

    [GeneratedRegex(@"\b(i|me|my|we|our|us|mine)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirstPersonPronounRegex();

    [GeneratedRegex(@"\([^)]{40,}\)", RegexOptions.CultureInvariant)]
    private static partial Regex LongParentheticalRegex();

    /// <summary>
    /// Evaluates a single instruction step against all filter rules.
    /// </summary>
    /// <param name="step">The instruction step to evaluate.</param>
    /// <returns>
    /// A <see cref="ProseFilterResult"/> indicating whether the step is retained
    /// or, if not, why it was dropped.
    /// </returns>
    public static ProseFilterResult Evaluate(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return new ProseFilterResult
            {
                DropReason = ProseFilterDropReason.Empty,
                Retained = false
            };
        }

        var cleaned = LongParentheticalRegex().Replace(step, string.Empty);

        if (FirstPersonPronounRegex().IsMatch(cleaned))
        {
            return new ProseFilterResult
            {
                DropReason = ProseFilterDropReason.FirstPersonPronoun,
                Retained = false
            };
        }

        var wordCount = cleaned.Split(WordSplitSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > MaxWordsPerStep)
        {
            return new ProseFilterResult
            {
                DropReason = ProseFilterDropReason.TooManyWords,
                Retained = false
            };
        }

        return new ProseFilterResult { Retained = true };
    }

    /// <summary>
    /// Filters a sequence of instruction steps, yielding only those that pass
    /// all filter rules. Preserves the original text and order of retained steps.
    /// </summary>
    /// <param name="steps">The ordered list of instruction steps from the source recipe.</param>
    /// <returns>An <see cref="IEnumerable{String}"/> containing only the retained steps.</returns>
    public static IEnumerable<string> FilterRetained(IEnumerable<string> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        foreach (var step in steps)
        {
            if (Evaluate(step).Retained)
            {
                yield return step;
            }
        }
    }
}
