using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MealsEnPlace.Api.Features.Inventory;

/// <summary>
/// Manages pantry, fridge, and freezer inventory items.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class InventoryController(
    IInventoryService inventoryService,
    UomDisplayConverter displayConverter) : ControllerBase
{
    /// <summary>
    /// Adds a new inventory item.
    /// </summary>
    /// <remarks>
    /// If the entry string in <c>Notes</c> contains a container keyword (can, jar, box, etc.)
    /// and no <c>DeclaredQuantity</c>/<c>DeclaredUomId</c> are provided, the server returns
    /// HTTP 200 with a <see cref="ContainerReferenceDetectedResponse"/> body instead of creating
    /// an item. The client should prompt the user for the net weight or volume, then re-submit
    /// with <c>DeclaredQuantity</c> and <c>DeclaredUomId</c> populated.
    /// </remarks>
    /// <param name="request">Item details including optional declared container size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// 201 with the created <see cref="InventoryItemResponse"/>, or
    /// 200 with a <see cref="ContainerReferenceDetectedResponse"/> when a container reference
    /// is detected and no declared size is provided.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ContainerReferenceDetectedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inventoryService.AddItemAsync(request, cancellationToken);

        if (result is ContainerReferenceDetectedResponse containerResponse)
        {
            return Ok(containerResponse);
        }

        var item = (InventoryItem)result;
        var response = await MapToResponseAsync(item, cancellationToken);
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, response);
    }

    /// <summary>
    /// Removes an inventory item by id.
    /// </summary>
    /// <param name="id">The id of the inventory item to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success; 404 if not found.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken cancellationToken)
    {
        var existing = await inventoryService.GetItemByIdAsync(id, cancellationToken);

        if (existing is null)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"Inventory item '{id}' was not found.",
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found"
            });
        }

        await inventoryService.DeleteItemAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns a single inventory item by id.
    /// </summary>
    /// <param name="id">The id of the inventory item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the item; 404 if not found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryItemResponse>> GetItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await inventoryService.GetItemByIdAsync(id, cancellationToken);

        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"Inventory item '{id}' was not found.",
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found"
            });
        }

        return Ok(await MapToResponseAsync(item, cancellationToken));
    }

    /// <summary>
    /// Returns all inventory items, optionally filtered by storage location.
    /// </summary>
    /// <param name="location">
    /// Optional filter: Pantry, Fridge, or Freezer.
    /// Omit to return all locations.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the list of items.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InventoryItemResponse>>> ListItems(
        [FromQuery] StorageLocation? location,
        CancellationToken cancellationToken)
    {
        var items = await inventoryService.ListItemsAsync(location, cancellationToken);
        var responses = new List<InventoryItemResponse>(items.Count);

        foreach (var item in items)
        {
            responses.Add(await MapToResponseAsync(item, cancellationToken));
        }

        return Ok(responses);
    }

    /// <summary>
    /// Updates an existing inventory item.
    /// </summary>
    /// <param name="id">The id of the inventory item to update.</param>
    /// <param name="request">Updated field values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with the updated item; 404 if not found.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InventoryItemResponse>> UpdateItem(
        Guid id,
        [FromBody] UpdateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await inventoryService.UpdateItemAsync(id, request, cancellationToken);

        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Detail = $"Inventory item '{id}' was not found.",
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found"
            });
        }

        return Ok(await MapToResponseAsync(item, cancellationToken));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<InventoryItemResponse> MapToResponseAsync(
        InventoryItem item,
        CancellationToken cancellationToken)
    {
        var uomType = item.Uom?.UomType ?? UomType.Arbitrary;
        var (displayQty, displayAbbr) = await displayConverter.ConvertAsync(
            item.Quantity, uomType, cancellationToken);

        return new InventoryItemResponse
        {
            CanonicalIngredientId = item.CanonicalIngredientId,
            CanonicalIngredientName = item.CanonicalIngredient?.Name ?? string.Empty,
            ExpiryDate = item.ExpiryDate,
            Id = item.Id,
            Location = item.Location,
            Notes = item.Notes,
            Quantity = displayQty,
            UomAbbreviation = displayAbbr,
            UomId = item.UomId
        };
    }
}
