namespace MealsEnPlace.Api.Common;

/// <summary>
/// Result returned by <see cref="ContainerReferenceDetector"/> after scanning an
/// ingredient or measure string for container keywords.
/// </summary>
public sealed class ContainerReferenceDetectionResult
{
    /// <summary>
    /// The container keyword that was detected (e.g., "can", "jar").
    /// Null when <see cref="IsContainerReference"/> is false.
    /// </summary>
    public string? DetectedKeyword { get; init; }

    /// <summary>
    /// True when a container keyword was found in the input string.
    /// False when the string can proceed to unit of measure parsing.
    /// </summary>
    public bool IsContainerReference { get; init; }

    /// <summary>
    /// The original input string that was evaluated.
    /// </summary>
    public string OriginalInput { get; init; } = string.Empty;

    /// <summary>
    /// Creates a negative result (no container reference detected).
    /// </summary>
    public static ContainerReferenceDetectionResult None(string input) =>
        new() { IsContainerReference = false, OriginalInput = input };

    /// <summary>
    /// Creates a positive result indicating a container reference was detected.
    /// </summary>
    public static ContainerReferenceDetectionResult Detected(string input, string keyword) =>
        new() { DetectedKeyword = keyword, IsContainerReference = true, OriginalInput = input };
}
