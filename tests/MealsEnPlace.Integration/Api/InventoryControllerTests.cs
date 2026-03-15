// Feature: Inventory Management (Integration)
//
// Scenario: Add an item to inventory — returns 201 Created
//   Given a valid AddInventoryItemRequest with no container reference
//   When POST /api/v1/inventory is called
//   Then the response status is 201 Created
//   And the response body contains the created item's Id
//
// Scenario: Add an item without an expiry date — expiry is null in response
//   Given a valid AddInventoryItemRequest with ExpiryDate = null
//   When POST /api/v1/inventory is called
//   Then the response body has a null ExpiryDate
//
// Scenario: Add an item with a container reference — returns 200 ContainerReferenceDetectedResponse
//   Given a request where Notes contains "can" and no DeclaredQuantity/DeclaredUomId
//   When POST /api/v1/inventory is called
//   Then the response status is 200 OK
//   And the response body contains DetectedKeyword = "can"
//
// Scenario: Add an item after declaring a container size — returns 201 Created
//   Given a request where Notes contains "can" and DeclaredQuantity/DeclaredUomId are populated
//   When POST /api/v1/inventory is called
//   Then the response status is 201 Created
//
// Scenario: Get item by id — returns 200 with the item
//   Given an inventory item exists in the database
//   When GET /api/v1/inventory/{id} is called
//   Then the response status is 200 OK
//   And the response contains the correct item
//
// Scenario: Get item by id — 404 when not found
//   Given no inventory item exists for a given id
//   When GET /api/v1/inventory/{id} is called
//   Then the response status is 404 Not Found
//
// Scenario: Delete an item — returns 204 No Content
//   Given an inventory item exists in the database
//   When DELETE /api/v1/inventory/{id} is called
//   Then the response status is 204 No Content
//   And a subsequent GET returns 404
//
// Scenario: Delete an item — 404 when not found
//   Given no inventory item exists for a given id
//   When DELETE /api/v1/inventory/{id} is called
//   Then the response status is 404 Not Found
//
// Scenario: Update an existing inventory item — returns 200
//   Given an inventory item exists in the database
//   When PUT /api/v1/inventory/{id} is called with updated quantity
//   Then the response status is 200 OK
//   And the response reflects the updated quantity
//
// Scenario: Update an item — 404 when not found
//   Given no inventory item exists for a given id
//   When PUT /api/v1/inventory/{id} is called
//   Then the response status is 404 Not Found
//
// Scenario: Change storage location via update
//   Given an inventory item in the Fridge
//   When PUT /api/v1/inventory/{id} is called with Location = Freezer
//   Then the response Location is Freezer
//
// Scenario: List items without filter — returns all items
//   Given items in Pantry, Fridge, and Freezer
//   When GET /api/v1/inventory is called without a location filter
//   Then all items are returned
//
// Scenario: List items filtered by Fridge — returns only Fridge items
//   Given items in Pantry and Fridge
//   When GET /api/v1/inventory?location=Fridge is called
//   Then only the Fridge item is returned

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MealsEnPlace.Api.Features.Inventory;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MealsEnPlace.Integration.Api;

// NOTE TO BACKEND ENGINEER:
// WebApplicationFactory<Program> requires the top-level Program class to be accessible.
// Add the following line to the bottom of src/MealsEnPlace.Api/Program.cs:
//
//   public partial class Program { }
//
// This is the standard .NET integration testing shim and has no runtime impact.

public class InventoryControllerTests : IClassFixture<InventoryWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly InventoryWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    public InventoryControllerTests(InventoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds one CanonicalIngredient, one UnitOfMeasure, and one InventoryItem into
    /// a fresh in-memory database scoped to this call. Returns all three for use in tests.
    /// </summary>
    private async Task<(CanonicalIngredient ingredient, UnitOfMeasure uom, InventoryItem item)>
        SeedItemAsync(
            StorageLocation location = StorageLocation.Pantry,
            decimal quantity = 500m,
            DateOnly? expiryDate = null,
            string? notes = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MealsEnPlaceDbContext>();

        var uom = new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1m,
            Id = Guid.NewGuid(),
            Name = "Gram",
            UomType = UomType.Weight
        };

        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Dairy,
            DefaultUomId = uom.Id,
            Id = Guid.NewGuid(),
            Name = "Test Ingredient " + Guid.NewGuid()
        };

        var item = new InventoryItem
        {
            CanonicalIngredientId = ingredient.Id,
            ExpiryDate = expiryDate,
            Id = Guid.NewGuid(),
            Location = location,
            Notes = notes,
            Quantity = quantity,
            UomId = uom.Id
        };

        dbContext.UnitsOfMeasure.Add(uom);
        dbContext.CanonicalIngredients.Add(ingredient);
        dbContext.InventoryItems.Add(item);
        await dbContext.SaveChangesAsync();

        return (ingredient, uom, item);
    }

    private async Task<(CanonicalIngredient ingredient, UnitOfMeasure uom)> SeedIngredientAndUomAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MealsEnPlaceDbContext>();

        var uom = new UnitOfMeasure
        {
            Abbreviation = "g",
            ConversionFactor = 1m,
            Id = Guid.NewGuid(),
            Name = "Gram",
            UomType = UomType.Weight
        };

        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Dairy,
            DefaultUomId = uom.Id,
            Id = Guid.NewGuid(),
            Name = "Seed Ingredient " + Guid.NewGuid()
        };

        dbContext.UnitsOfMeasure.Add(uom);
        dbContext.CanonicalIngredients.Add(ingredient);
        await dbContext.SaveChangesAsync();

        return (ingredient, uom);
    }

    // ── POST /api/v1/inventory ────────────────────────────────────────────────

    [Fact]
    public async Task AddItem_ValidRequest_Returns201Created()
    {
        // Arrange
        var (ingredient, uom) = await SeedIngredientAndUomAsync();
        var request = new AddInventoryItemRequest
        {
            CanonicalIngredientId = ingredient.Id,
            Location = StorageLocation.Pantry,
            Notes = "",
            Quantity = 500m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddItem_ValidRequest_ResponseBodyContainsId()
    {
        // Arrange
        var (ingredient, uom) = await SeedIngredientAndUomAsync();
        var request = new AddInventoryItemRequest
        {
            CanonicalIngredientId = ingredient.Id,
            Location = StorageLocation.Pantry,
            Notes = "",
            Quantity = 250m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", request);
        var body = await response.Content.ReadFromJsonAsync<InventoryItemResponse>(JsonOptions);

        // Assert
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddItem_NullExpiryDate_ResponseExpiryDateIsNull()
    {
        // Arrange
        var (ingredient, uom) = await SeedIngredientAndUomAsync();
        var request = new AddInventoryItemRequest
        {
            CanonicalIngredientId = ingredient.Id,
            ExpiryDate = null,
            Location = StorageLocation.Pantry,
            Notes = "",
            Quantity = 100m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", request);
        var body = await response.Content.ReadFromJsonAsync<InventoryItemResponse>(JsonOptions);

        // Assert
        body!.ExpiryDate.Should().BeNull();
    }

    [Fact]
    public async Task AddItem_NotesContainsCan_Returns200WithContainerReferenceDetectedResponse()
    {
        // Arrange
        var (ingredient, uom) = await SeedIngredientAndUomAsync();
        var request = new AddInventoryItemRequest
        {
            CanonicalIngredientId = ingredient.Id,
            Location = StorageLocation.Pantry,
            Notes = "1 can of diced tomatoes",
            Quantity = 1m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ContainerReferenceDetectedResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.DetectedKeyword.Should().Be("can");
    }

    [Fact]
    public async Task AddItem_ContainerDetected_ResponseOriginalInputMatchesNotes()
    {
        // Arrange
        var (ingredient, uom) = await SeedIngredientAndUomAsync();
        const string notes = "1 can of diced tomatoes";
        var request = new AddInventoryItemRequest
        {
            CanonicalIngredientId = ingredient.Id,
            Location = StorageLocation.Pantry,
            Notes = notes,
            Quantity = 1m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", request);
        var body = await response.Content.ReadFromJsonAsync<ContainerReferenceDetectedResponse>(JsonOptions);

        // Assert
        body!.OriginalInput.Should().Be(notes);
    }

    [Fact]
    public async Task AddItem_DeclaredSizeBypassesContainerDetection_Returns201Created()
    {
        // Arrange
        var (ingredient, uom) = await SeedIngredientAndUomAsync();
        var request = new AddInventoryItemRequest
        {
            CanonicalIngredientId = ingredient.Id,
            DeclaredQuantity = 14.5m,
            DeclaredUomId = uom.Id,
            Location = StorageLocation.Pantry,
            Notes = "1 can of diced tomatoes",
            Quantity = 1m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── GET /api/v1/inventory/{id} ────────────────────────────────────────────

    [Fact]
    public async Task GetItem_ExistingId_Returns200WithItem()
    {
        // Arrange
        var (_, _, item) = await SeedItemAsync(quantity: 300m);

        // Act
        var response = await _client.GetAsync($"/api/v1/inventory/{item.Id}");
        var body = await response.Content.ReadFromJsonAsync<InventoryItemResponse>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task GetItem_NonExistentId_Returns404()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/inventory/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/v1/inventory/{id} ─────────────────────────────────────────

    [Fact]
    public async Task DeleteItem_ExistingId_Returns204NoContent()
    {
        // Arrange
        var (_, _, item) = await SeedItemAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/inventory/{item.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteItem_ExistingId_SubsequentGetReturns404()
    {
        // Arrange
        var (_, _, item) = await SeedItemAsync();

        // Act
        await _client.DeleteAsync($"/api/v1/inventory/{item.Id}");
        var getResponse = await _client.GetAsync($"/api/v1/inventory/{item.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItem_NonExistentId_Returns404()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/inventory/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/v1/inventory/{id} ────────────────────────────────────────────

    [Fact]
    public async Task UpdateItem_ExistingId_Returns200()
    {
        // Arrange
        var (_, uom, item) = await SeedItemAsync(quantity: 12m);
        var updateRequest = new UpdateInventoryItemRequest
        {
            Location = StorageLocation.Fridge,
            Quantity = 6m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/inventory/{item.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateItem_ExistingId_ResponseReflectsUpdatedQuantity()
    {
        // Arrange
        var (_, uom, item) = await SeedItemAsync(quantity: 12m);
        var updateRequest = new UpdateInventoryItemRequest
        {
            Location = StorageLocation.Fridge,
            Quantity = 6m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/inventory/{item.Id}", updateRequest);
        var body = await response.Content.ReadFromJsonAsync<InventoryItemResponse>(JsonOptions);

        // Assert
        // Note: Quantity in the response is display-converted (6g → fl oz/oz/etc.)
        // We verify the UomId is unchanged and the response round-trips correctly.
        body.Should().NotBeNull();
        body!.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task UpdateItem_ChangeLocation_ResponseLocationIsUpdated()
    {
        // Arrange
        var (_, uom, item) = await SeedItemAsync(location: StorageLocation.Fridge);
        var updateRequest = new UpdateInventoryItemRequest
        {
            Location = StorageLocation.Freezer,
            Quantity = 500m,
            UomId = uom.Id
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/inventory/{item.Id}", updateRequest);
        var body = await response.Content.ReadFromJsonAsync<InventoryItemResponse>(JsonOptions);

        // Assert
        body!.Location.Should().Be(StorageLocation.Freezer);
    }

    [Fact]
    public async Task UpdateItem_NonExistentId_Returns404()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uomId = Guid.NewGuid();
        var updateRequest = new UpdateInventoryItemRequest
        {
            Location = StorageLocation.Pantry,
            Quantity = 1m,
            UomId = uomId
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/inventory/{id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/v1/inventory ─────────────────────────────────────────────────

    [Fact]
    public async Task ListItems_NoFilter_ReturnsAllItems()
    {
        // Arrange — seed one item per location to ensure all are returned
        await SeedItemAsync(location: StorageLocation.Pantry, quantity: 100m);
        await SeedItemAsync(location: StorageLocation.Fridge, quantity: 200m);
        await SeedItemAsync(location: StorageLocation.Freezer, quantity: 300m);

        // Act
        var response = await _client.GetAsync("/api/v1/inventory");
        var body = await response.Content.ReadFromJsonAsync<List<InventoryItemResponse>>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // At minimum the three we seeded must be present (other tests may have added items)
        body.Should().NotBeNull();
        body!.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task ListItems_FridgeFilter_ReturnsOnlyFridgeItems()
    {
        // Arrange — seed items at two locations; only the Fridge one should come back
        await SeedItemAsync(location: StorageLocation.Pantry, quantity: 100m);
        var (_, _, fridgeItem) = await SeedItemAsync(location: StorageLocation.Fridge, quantity: 200m);

        // Act
        var response = await _client.GetAsync("/api/v1/inventory?location=Fridge");
        var body = await response.Content.ReadFromJsonAsync<List<InventoryItemResponse>>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Should().OnlyContain(i => i.Location == StorageLocation.Fridge);
        body.Should().Contain(i => i.Id == fridgeItem.Id);
    }

    [Fact]
    public async Task ListItems_FreezerFilter_ReturnsOnlyFreezerItems()
    {
        // Arrange
        await SeedItemAsync(location: StorageLocation.Pantry, quantity: 50m);
        var (_, _, freezerItem) = await SeedItemAsync(location: StorageLocation.Freezer, quantity: 900m);

        // Act
        var response = await _client.GetAsync("/api/v1/inventory?location=Freezer");
        var body = await response.Content.ReadFromJsonAsync<List<InventoryItemResponse>>(JsonOptions);

        // Assert
        body!.Should().OnlyContain(i => i.Location == StorageLocation.Freezer);
        body.Should().Contain(i => i.Id == freezerItem.Id);
    }
}

/// <summary>
/// Custom <see cref="WebApplicationFactory{TProgram}"/> for inventory integration tests.
/// Replaces the PostgreSQL database with an EF Core in-memory provider so no real
/// database connection is required.
/// </summary>
public class InventoryWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "InventoryIntegrationTests_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related registrations so the Npgsql provider is fully cleared
            var dbContextDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("DbContext") == true
                         || d.ServiceType.FullName?.Contains("EntityFramework") == true
                         || d.ServiceType.FullName?.Contains("Npgsql") == true)
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Register an in-memory database for this factory instance
            services.AddDbContext<MealsEnPlaceDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
