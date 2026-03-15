using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Models.Entities;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Default implementation of <see cref="IInventoryService"/>.
/// Runs container reference detection before creating items; delegates persistence
/// to <see cref="IInventoryRepository"/>.
/// </summary>
public class InventoryService(IInventoryRepository repository) : IInventoryService
{
    /// <inheritdoc />
    public async Task<object> AddItemAsync(
        AddInventoryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        // When the user has already declared a container size, skip detection.
        var hasDeclaredSize = request.DeclaredQuantity.HasValue && request.DeclaredUomId.HasValue;

        if (!hasDeclaredSize)
        {
            var detection = ContainerReferenceDetector.Detect(request.Notes);

            if (detection.IsContainerReference)
            {
                return new ContainerReferenceDetectedResponse
                {
                    DetectedKeyword = detection.DetectedKeyword!,
                    Message = $"A container reference (\"{detection.DetectedKeyword}\") was detected. "
                              + "What is the net weight or volume of this container?",
                    OriginalInput = detection.OriginalInput
                };
            }
        }

        var item = new InventoryItem
        {
            CanonicalIngredientId = request.CanonicalIngredientId,
            ExpiryDate = request.ExpiryDate,
            Id = Guid.NewGuid(),
            Location = request.Location,
            Notes = hasDeclaredSize ? request.Notes : null,
            Quantity = hasDeclaredSize ? request.DeclaredQuantity!.Value : request.Quantity,
            UomId = hasDeclaredSize ? request.DeclaredUomId!.Value : request.UomId
        };

        return await repository.AddAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        await repository.DeleteAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<InventoryItem?> GetItemByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<InventoryItem>> ListItemsAsync(
        StorageLocation? location,
        CancellationToken cancellationToken = default) =>
        await repository.ListAsync(location, cancellationToken);

    /// <inheritdoc />
    public async Task<InventoryItem?> UpdateItemAsync(
        Guid id,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken = default) =>
        await repository.UpdateAsync(id, request, cancellationToken);
}
