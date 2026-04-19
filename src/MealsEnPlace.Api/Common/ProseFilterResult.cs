namespace MealsEnPlace.Api.Common;

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
