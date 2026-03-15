namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Returned by <c>POST /api/v1/inventory</c> when the entry string contains a container
/// keyword. The client should prompt the user to declare the net weight or volume, then
/// re-submit the POST with <c>DeclaredQuantity</c> and <c>DeclaredUomId</c> populated.
/// </summary>
public sealed class ContainerReferenceDetectedResponse
{
    /// <summary>
    /// The container keyword that triggered the detection (e.g., "can", "jar").
    /// </summary>
    public string DetectedKeyword { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable prompt to display to the user asking them to declare
    /// the net weight or volume.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The original entry string submitted by the user, to be preserved in the
    /// Notes field once the container size is declared.
    /// </summary>
    public string OriginalInput { get; init; } = string.Empty;
}
