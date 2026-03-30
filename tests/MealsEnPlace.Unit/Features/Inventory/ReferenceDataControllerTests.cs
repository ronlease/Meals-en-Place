// Feature: Reference Data Controller
//
// Scenario: Create ingredient with blank name — returns 400 Bad Request
//   Given a request with an empty or whitespace Name
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 400 Bad Request
//
// Scenario: Create ingredient with UOM not found — returns 400 Bad Request
//   Given a request with a DefaultUomId that does not exist in the database
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 400 Bad Request
//
// Scenario: Create ingredient with duplicate name — returns 409 Conflict
//   Given an ingredient with the same name already exists
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 409 Conflict
//
// Scenario: Create ingredient with valid request — returns 201 Created
//   Given a valid request with a unique name and an existing DefaultUomId
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 201 Created with the new ingredient DTO
//
// Scenario: List ingredients — returns 200 with ordered list
//   Given canonical ingredients exist in the database
//   When GET /api/v1/reference-data/ingredients is called
//   Then the response is 200 OK with ingredients ordered by name
//
// Scenario: List units — returns 200 with ordered list
//   Given units of measure exist in the database
//   When GET /api/v1/reference-data/units is called
//   Then the response is 200 OK with units ordered by name

using FluentAssertions;
using MealsEnPlace.Api.Features.Inventory;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealsEnPlace.Unit.Features.Inventory;

public class ReferenceDataControllerTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly MealsEnPlaceDbContext _dbContext;
    private readonly ReferenceDataController _sut;

    public ReferenceDataControllerTests()
    {
        var options = new DbContextOptionsBuilder<MealsEnPlaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MealsEnPlaceDbContext(options);
        _sut = new ReferenceDataController(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── CreateIngredient — blank name ─────────────────────────────────────────

    [Fact]
    public async Task CreateIngredient_BlankName_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = Guid.NewGuid(),
            Name = string.Empty
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateIngredient_WhitespaceName_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = Guid.NewGuid(),
            Name = "   "
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ── CreateIngredient — UOM not found ──────────────────────────────────────

    [Fact]
    public async Task CreateIngredient_UomNotFound_Returns400BadRequest()
    {
        // Arrange — DefaultUomId references a UOM that does not exist in the DB
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = Guid.NewGuid(),
            Name = "Tomato"
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateIngredient_UomNotFound_ProblemDetailContainsUomId()
    {
        // Arrange
        var missingUomId = Guid.NewGuid();
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = missingUomId,
            Name = "Tomato"
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain(missingUomId.ToString());
    }

    // ── CreateIngredient — duplicate name ─────────────────────────────────────

    [Fact]
    public async Task CreateIngredient_DuplicateName_Returns409Conflict()
    {
        // Arrange — seed a UOM and an existing ingredient
        var uom = await SeedUomAsync();
        await SeedIngredientAsync("Garlic", uom.Id);

        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = uom.Id,
            Name = "Garlic"
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>()
            .Which.StatusCode.Should().Be(409);
    }

    // ── CreateIngredient — valid request ──────────────────────────────────────

    [Fact]
    public async Task CreateIngredient_ValidRequest_Returns201Created()
    {
        // Arrange
        var uom = await SeedUomAsync();
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = uom.Id,
            Name = "Spinach"
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreateIngredient_ValidRequest_ResponseBodyContainsIngredientDto()
    {
        // Arrange
        var uom = await SeedUomAsync();
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = uom.Id,
            Name = "Kale"
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<CanonicalIngredientDto>().Subject;
        dto.Name.Should().Be("Kale");
        dto.Category.Should().Be(IngredientCategory.Produce);
    }

    // ── ListIngredients ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListIngredients_IngredientsExist_Returns200WithOrderedList()
    {
        // Arrange
        var uom = await SeedUomAsync();
        await SeedIngredientAsync("Zucchini", uom.Id);
        await SeedIngredientAsync("Apple", uom.Id);
        await SeedIngredientAsync("Mango", uom.Id);

        // Act
        var result = await _sut.ListIngredients(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(3);
        list.Select(i => i.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListIngredients_NoIngredients_Returns200WithEmptyList()
    {
        // Act
        var result = await _sut.ListIngredients(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>()
            .Which.Should().BeEmpty();
    }

    // ── ListUnits ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListUnits_UnitsExist_Returns200WithOrderedList()
    {
        // Arrange
        await SeedUomWithNameAsync("Teaspoon");
        await SeedUomWithNameAsync("Cup");
        await SeedUomWithNameAsync("Gram");

        // Act
        var result = await _sut.ListUnits(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<UnitOfMeasureDto>>().Subject;
        list.Should().HaveCount(3);
        list.Select(u => u.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListUnits_NoUnits_Returns200WithEmptyList()
    {
        // Act
        var result = await _sut.ListUnits(CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeAssignableTo<IReadOnlyList<UnitOfMeasureDto>>()
            .Which.Should().BeEmpty();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task<CanonicalIngredient> SeedIngredientAsync(string name, Guid uomId)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Produce,
            DefaultUomId = uomId,
            Id = Guid.NewGuid(),
            Name = name
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        await _dbContext.SaveChangesAsync();
        return ingredient;
    }

    private async Task<UnitOfMeasure> SeedUomAsync()
    {
        return await SeedUomWithNameAsync("gram");
    }

    private async Task<UnitOfMeasure> SeedUomWithNameAsync(string name)
    {
        var uom = new UnitOfMeasure
        {
            Abbreviation = name[..1].ToLowerInvariant(),
            ConversionFactor = 1.0m,
            Id = Guid.NewGuid(),
            Name = name,
            UomType = UomType.Weight
        };
        _dbContext.UnitsOfMeasure.Add(uom);
        await _dbContext.SaveChangesAsync();
        return uom;
    }
}
