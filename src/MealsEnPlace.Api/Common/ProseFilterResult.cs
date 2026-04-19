namespace MealsEnPlace.Api.Common;

/// <summary>
/// Reason an instruction step was dropped by <see cref="InstructionProseFilter"/>.
/// </summary>
public enum ProseFilterDropReason
{
    /// <summary>The step was null, empty, or whitespace-only.</summary>
    Empty,

    /// <summary>
    /// The step contained a first-person pronoun (I, me, my, we, our, us, mine),
    /// which is a strong signal of blog-voice narrative rather than a
    /// functional recipe instruction.
    /// </summary>
    FirstPersonPronoun,

    /// <summary>
    /// The step exceeded the maximum allowed word count after long
    /// parentheticals were stripped. Long steps are almost always narrative
    /// drift or a merged multi-step block.
    /// </summary>
    TooManyWords
}

/// <summary>
/// Result returned by <see cref="InstructionProseFilter.Evaluate"/>.
/// </summary>
public sealed class ProseFilterResult
{
    /// <summary>
    /// When <see cref="Retained"/> is false, the reason the step was dropped.
    /// Null when the step was retained.
    /// </summary>
    public ProseFilterDropReason? DropReason { get; init; }

    /// <summary>True if the step passed all filter rules and should be kept.</summary>
    public bool Retained { get; init; }
}
