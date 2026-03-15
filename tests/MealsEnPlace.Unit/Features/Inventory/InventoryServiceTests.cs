// Feature: Inventory Management
//
// Scenario: Add an item to inventory
//   Given a valid AddInventoryItemRequest with no container reference in Notes
//   When AddItemAsync is called
//   Then the repository AddAsync is called once
//   And the returned result is an InventoryItem
//
// Scenario: Add an item without an expiry date
//   Given a valid AddInventoryItemRequest with ExpiryDate = null
//   When AddItemAsync is called
//   Then the created InventoryItem has ExpiryDate = null
//
// Scenario: Container reference detected — returns ContainerReferenceDetectedResponse
//   Given a request where Notes contains "can" and no DeclaredQuantity/DeclaredUomId
//   When AddItemAsync is called
//   Then a ContainerReferenceDetectedResponse is returned
//   And the repository AddAsync is never called
//
// Scenario: Container reference bypassed when declared size is provided
//   Given a request where Notes contains "can" but DeclaredQuantity and DeclaredUomId are set
//   When AddItemAsync is called
//   Then the repository AddAsync is called once
//   And the InventoryItem stores the DeclaredQuantity and DeclaredUomId
//
// Scenario: Container reference detected — response contains OriginalInput
//   Given a request with Notes "1 can of diced tomatoes" and no declared size
//   When AddItemAsync is called and detection fires
//   Then ContainerReferenceDetectedResponse.OriginalInput equals Notes
//
// Scenario: Container reference detected — response contains DetectedKeyword
//   Given a request with Notes containing "jar"
//   When AddItemAsync returns a ContainerReferenceDetectedResponse
//   Then DetectedKeyword is "jar"
//
// Scenario: After container declaration, Notes is preserved on the InventoryItem
//   Given a request with Notes "1 can of diced tomatoes" and DeclaredQuantity/DeclaredUomId set
//   When AddItemAsync is called
//   Then the saved InventoryItem.Notes equals "1 can of diced tomatoes"
//
// Scenario: Without container declaration, Notes on InventoryItem is null
//   Given a plain request (no container in Notes, no DeclaredQuantity)
//   When AddItemAsync is called
//   Then the saved InventoryItem.Notes is null
//
// Scenario: Edit an existing inventory item — quantity change
//   Given an existing inventory item
//   When UpdateItemAsync is called with a new quantity
//   Then the repository UpdateAsync is called
//
// Scenario: Change an item's storage location
//   Given an existing inventory item with Location = Fridge
//   When UpdateItemAsync is called with Location = Freezer
//   Then the repository UpdateAsync is called with the new location
//
// Scenario: Delete an item
//   When DeleteItemAsync is called with an id
//   Then the repository DeleteAsync is called with that id
//
// Scenario: List items without location filter
//   When ListItemsAsync is called with null location
//   Then the repository ListAsync is called with null
//
// Scenario: List items filtered by location
//   When ListItemsAsync is called with location = Fridge
//   Then the repository ListAsync is called with location = Fridge
//
// Scenario: Get item by id — delegates to repository
//   When GetItemByIdAsync is called with an id
//   Then the repository GetByIdAsync is called with that id

using FluentAssertions;
using MealsEnPlace.Api.Features.Inventory;
using MealsEnPlace.Api.Models.Entities;
using Moq;

namespace MealsEnPlace.Unit.Features.Inventory;

public class InventoryServiceTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IInventoryRepository> _repositoryMock = new(MockBehavior.Strict);
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        _sut = new InventoryService(_repositoryMock.Object);
    }

    private static readonly Guid IngredientId = Guid.NewGuid();
    private static readonly Guid UomId = Guid.NewGuid();

    private static AddInventoryItemRequest BuildPlainRequest(
        StorageLocation location = StorageLocation.Pantry,
        decimal quantity = 500m,
        string notes = "",
        DateOnly? expiryDate = null) =>
        new()
        {
            CanonicalIngredientId = IngredientId,
            ExpiryDate = expiryDate,
            Location = location,
            Notes = notes,
            Quantity = quantity,
            UomId = UomId
        };

    private static AddInventoryItemRequest BuildDeclaredRequest(
        string notes = "1 can of diced tomatoes",
        decimal declaredQty = 14.5m,
        Guid? declaredUomId = null) =>
        new()
        {
            CanonicalIngredientId = IngredientId,
            DeclaredQuantity = declaredQty,
            DeclaredUomId = declaredUomId ?? UomId,
            Location = StorageLocation.Pantry,
            Notes = notes,
            Quantity = 1m,
            UomId = UomId
        };

    private static InventoryItem BuildSavedItem(Guid? id = null) =>
        new()
        {
            CanonicalIngredientId = IngredientId,
            Id = id ?? Guid.NewGuid(),
            Location = StorageLocation.Pantry,
            Quantity = 500m,
            UomId = UomId
        };

    // ── AddItemAsync — plain item (no container reference) ───────────────────

    [Fact]
    public async Task AddItemAsync_PlainRequest_CallsRepositoryAddAsync()
    {
        // Arrange
        var request = BuildPlainRequest();
        var savedItem = BuildSavedItem();
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(savedItem);

        // Act
        var result = await _sut.AddItemAsync(request);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Should().BeOfType<InventoryItem>();
    }

    [Fact]
    public async Task AddItemAsync_PlainRequest_ReturnsInventoryItem()
    {
        // Arrange
        var request = BuildPlainRequest();
        var savedItem = BuildSavedItem();
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(savedItem);

        // Act
        var result = await _sut.AddItemAsync(request);

        // Assert
        result.Should().BeOfType<InventoryItem>();
    }

    [Fact]
    public async Task AddItemAsync_RequestWithNullExpiryDate_CreatesItemWithNullExpiryDate()
    {
        // Arrange
        var request = BuildPlainRequest(expiryDate: null);
        InventoryItem? captured = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .Callback<InventoryItem, CancellationToken>((item, _) => captured = item)
                       .ReturnsAsync((InventoryItem item, CancellationToken _) => item);

        // Act
        await _sut.AddItemAsync(request);

        // Assert
        captured.Should().NotBeNull();
        captured!.ExpiryDate.Should().BeNull();
    }

    [Fact]
    public async Task AddItemAsync_PlainRequest_NotesOnSavedItemIsNull()
    {
        // Arrange — plain request has no DeclaredQuantity, no container keyword
        var request = BuildPlainRequest(notes: "");
        InventoryItem? captured = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .Callback<InventoryItem, CancellationToken>((item, _) => captured = item)
                       .ReturnsAsync((InventoryItem item, CancellationToken _) => item);

        // Act
        await _sut.AddItemAsync(request);

        // Assert
        captured!.Notes.Should().BeNull();
    }

    // ── AddItemAsync — container reference detected ───────────────────────────

    [Fact]
    public async Task AddItemAsync_NotesContainsCanKeywordNoDeclaredSize_ReturnsContainerReferenceDetectedResponse()
    {
        // Arrange
        var request = BuildPlainRequest(notes: "1 can of diced tomatoes");

        // Act
        var result = await _sut.AddItemAsync(request);

        // Assert
        result.Should().BeOfType<ContainerReferenceDetectedResponse>();
    }

    [Fact]
    public async Task AddItemAsync_NotesContainsCanKeywordNoDeclaredSize_RepositoryNeverCalled()
    {
        // Arrange
        var request = BuildPlainRequest(notes: "1 can of diced tomatoes");

        // Act
        await _sut.AddItemAsync(request);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_NotesContainsJarKeyword_ResponseContainsJarAsDetectedKeyword()
    {
        // Arrange
        var request = BuildPlainRequest(notes: "1 jar of marinara sauce");

        // Act
        var result = await _sut.AddItemAsync(request);

        // Assert
        var containerResponse = result.Should().BeOfType<ContainerReferenceDetectedResponse>().Subject;
        containerResponse.DetectedKeyword.Should().Be("jar");
    }

    [Fact]
    public async Task AddItemAsync_ContainerDetected_ResponseOriginalInputMatchesNotes()
    {
        // Arrange
        const string notes = "1 can of diced tomatoes";
        var request = BuildPlainRequest(notes: notes);

        // Act
        var result = await _sut.AddItemAsync(request);

        // Assert
        var containerResponse = result.Should().BeOfType<ContainerReferenceDetectedResponse>().Subject;
        containerResponse.OriginalInput.Should().Be(notes);
    }

    [Fact]
    public async Task AddItemAsync_ContainerDetected_ResponseMessageIsNotEmpty()
    {
        // Arrange
        var request = BuildPlainRequest(notes: "1 can of diced tomatoes");

        // Act
        var result = await _sut.AddItemAsync(request);

        // Assert
        var containerResponse = result.Should().BeOfType<ContainerReferenceDetectedResponse>().Subject;
        containerResponse.Message.Should().NotBeNullOrWhiteSpace();
    }

    // ── AddItemAsync — container reference bypassed with declared size ─────────

    [Fact]
    public async Task AddItemAsync_DeclaredSizeProvided_ContainerDetectionSkipped_ItemCreated()
    {
        // Arrange — Notes contains "can" but DeclaredQuantity/DeclaredUomId bypass detection
        var request = BuildDeclaredRequest(notes: "1 can of diced tomatoes", declaredQty: 14.5m);
        var savedItem = BuildSavedItem();
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(savedItem);

        // Act
        var result = await _sut.AddItemAsync(request);

        // Assert
        result.Should().BeOfType<InventoryItem>();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_DeclaredSizeProvided_SavedItemUsesDeclaredQuantity()
    {
        // Arrange
        const decimal declaredQty = 14.5m;
        var request = BuildDeclaredRequest(notes: "1 can of diced tomatoes", declaredQty: declaredQty);
        InventoryItem? captured = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .Callback<InventoryItem, CancellationToken>((item, _) => captured = item)
                       .ReturnsAsync((InventoryItem item, CancellationToken _) => item);

        // Act
        await _sut.AddItemAsync(request);

        // Assert
        captured!.Quantity.Should().Be(declaredQty);
    }

    [Fact]
    public async Task AddItemAsync_DeclaredSizeProvided_SavedItemUsesDeclaredUomId()
    {
        // Arrange
        var declaredUomId = Guid.NewGuid();
        var request = BuildDeclaredRequest(notes: "1 can of diced tomatoes", declaredUomId: declaredUomId);
        InventoryItem? captured = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .Callback<InventoryItem, CancellationToken>((item, _) => captured = item)
                       .ReturnsAsync((InventoryItem item, CancellationToken _) => item);

        // Act
        await _sut.AddItemAsync(request);

        // Assert
        captured!.UomId.Should().Be(declaredUomId);
    }

    [Fact]
    public async Task AddItemAsync_DeclaredSizeProvided_SavedItemPreservesNotesFromRequest()
    {
        // Arrange
        const string notes = "1 can of diced tomatoes";
        var request = BuildDeclaredRequest(notes: notes);
        InventoryItem? captured = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .Callback<InventoryItem, CancellationToken>((item, _) => captured = item)
                       .ReturnsAsync((InventoryItem item, CancellationToken _) => item);

        // Act
        await _sut.AddItemAsync(request);

        // Assert
        captured!.Notes.Should().Be(notes);
    }

    // ── AddItemAsync — location and ingredient are propagated ─────────────────

    [Fact]
    public async Task AddItemAsync_FreezerlocationRequest_SavedItemHasFreezerLocation()
    {
        // Arrange
        var request = BuildPlainRequest(location: StorageLocation.Freezer);
        InventoryItem? captured = null;
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                       .Callback<InventoryItem, CancellationToken>((item, _) => captured = item)
                       .ReturnsAsync((InventoryItem item, CancellationToken _) => item);

        // Act
        await _sut.AddItemAsync(request);

        // Assert
        captured!.Location.Should().Be(StorageLocation.Freezer);
    }

    // ── DeleteItemAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteItemAsync_CallsRepositoryDeleteAsyncWithCorrectId()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteItemAsync(id);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetItemByIdAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemByIdAsync_CallsRepositoryGetByIdAsyncWithCorrectId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var item = BuildSavedItem(id);
        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(item);

        // Act
        var result = await _sut.GetItemByIdAsync(id);

        // Assert
        _repositoryMock.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        result.Should().Be(item);
    }

    [Fact]
    public async Task GetItemByIdAsync_RepositoryReturnsNull_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((InventoryItem?)null);

        // Act
        var result = await _sut.GetItemByIdAsync(id);

        // Assert
        result.Should().BeNull();
    }

    // ── ListItemsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListItemsAsync_NullLocation_PassesNullToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Array.Empty<InventoryItem>());

        // Act
        await _sut.ListItemsAsync(null);

        // Assert
        _repositoryMock.Verify(r => r.ListAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListItemsAsync_FridgeLocation_PassesFridgeToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.ListAsync(StorageLocation.Fridge, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Array.Empty<InventoryItem>());

        // Act
        await _sut.ListItemsAsync(StorageLocation.Fridge);

        // Assert
        _repositoryMock.Verify(r => r.ListAsync(StorageLocation.Fridge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListItemsAsync_ReturnsItemsFromRepository()
    {
        // Arrange
        var items = new[] { BuildSavedItem(), BuildSavedItem() };
        _repositoryMock.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(items);

        // Act
        var result = await _sut.ListItemsAsync(null);

        // Assert
        result.Should().HaveCount(2);
    }

    // ── UpdateItemAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateItemAsync_CallsRepositoryUpdateAsyncWithCorrectId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateInventoryItemRequest { Location = StorageLocation.Freezer, Quantity = 6m, UomId = UomId };
        var updated = BuildSavedItem(id);
        _repositoryMock.Setup(r => r.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(updated);

        // Act
        var result = await _sut.UpdateItemAsync(id, request);

        // Assert
        _repositoryMock.Verify(r => r.UpdateAsync(id, request, It.IsAny<CancellationToken>()), Times.Once);
        result.Should().Be(updated);
    }

    [Fact]
    public async Task UpdateItemAsync_RepositoryReturnsNull_ReturnsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateInventoryItemRequest { Location = StorageLocation.Fridge, Quantity = 1m, UomId = UomId };
        _repositoryMock.Setup(r => r.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((InventoryItem?)null);

        // Act
        var result = await _sut.UpdateItemAsync(id, request);

        // Assert
        result.Should().BeNull();
    }
}
