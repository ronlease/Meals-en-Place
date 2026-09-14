// Feature: Reference Data Controller
//
// Scenario: Create ingredient with blank name — returns 400 Bad Request
//   Given a request with an empty or whitespace Name
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 400 Bad Request
//
// Scenario: Create ingredient with unit of measure not found — returns 400 Bad Request
//   Given a request with a DefaultUnitOfMeasureId that does not exist in the database
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 400 Bad Request
//
// Scenario: Create ingredient with duplicate name — returns 409 Conflict
//   Given an ingredient with the same name already exists
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 409 Conflict
//
// Scenario: Create ingredient with valid request — returns 201 Created
//   Given a valid request with a unique name and an existing DefaultUnitOfMeasureId
//   When POST /api/v1/reference-data/ingredients is called
//   Then the response is 201 Created with the new ingredient DTO
//
// Scenario: Search ingredients — blank search returns empty list
//   Given the CanonicalIngredients table has rows
//   When GET /api/v1/reference-data/ingredients is called with an empty or whitespace search term
//   Then the response is 200 OK with an empty array
//
// Scenario: Search ingredients — whitespace search returns empty list
//   Given the CanonicalIngredients table has rows
//   When GET /api/v1/reference-data/ingredients is called with search="   "
//   Then the response is 200 OK with an empty array
//
// Scenario: Search ingredients — default limit is 20
//   Given more than 20 matching canonical ingredients exist
//   When GET /api/v1/reference-data/ingredients is called without specifying a limit
//   Then the response contains at most 20 items
//
// Scenario: Search ingredients — limit above 50 is clamped to 50
//   Given more than 50 matching canonical ingredients exist
//   When GET /api/v1/reference-data/ingredients is called with limit=500
//   Then the response contains at most 50 items
//
// Scenario: Search ingredients — limit below 1 is clamped to 1
//   Given at least 2 matching canonical ingredients exist
//   When GET /api/v1/reference-data/ingredients is called with limit=0 or limit=-5
//   Then the response contains exactly 1 item
//
// Scenario: Search ingredients — matching is case-insensitive
//   Given a canonical ingredient named "Chicken Breast" exists
//   When GET /api/v1/reference-data/ingredients is called with search="chicken breast"
//   Then "Chicken Breast" appears in the results
//
// Scenario: Search ingredients — prefix matches are ranked before substring matches
//   Given canonical ingredients "Garlic" and "Roasted Garlic Hummus" exist
//   When GET /api/v1/reference-data/ingredients is called with search="garlic"
//   Then "Garlic" appears before "Roasted Garlic Hummus" in the results
//
// Scenario: Search ingredients — ties are broken alphabetically by name
//   Given canonical ingredients "Spinach" and "Baby Spinach" both start with or contain the search term
//   When the results contain both at the same rank tier
//   Then they appear in ascending alphabetical order
//
// Scenario: Search ingredients — wildcard characters in the term are treated literally
//   Given a canonical ingredient named "100% Whole Wheat Flour" exists
//   And canonical ingredients "Garlic" and "Butter" also exist
//   When GET /api/v1/reference-data/ingredients is called with search="100%"
//   Then only "100% Whole Wheat Flour" is returned
//
// Scenario: Search ingredients — response contains only DTO fields
//   Given a canonical ingredient exists
//   When GET /api/v1/reference-data/ingredients is called with a matching search term
//   Then each item in the response has Id, Name, Category, and DefaultUnitOfMeasureId
//
// Scenario: Search ingredients — more-referenced prefix match ranks above less-referenced prefix match
//   Given prefix-match ingredients "Apple" (RecipeReferenceCount = 10) and "Apple Cider" (RecipeReferenceCount = 2)
//   When GET /api/v1/reference-data/ingredients is called with search="apple"
//   Then "Apple" appears before "Apple Cider" in the results
//
// Scenario: Search ingredients — prefix match with zero references still ranks above contains match with many references
//   Given "Apple" (RecipeReferenceCount = 0, prefix match) and "Pineapple" (RecipeReferenceCount = 50, contains match)
//   When GET /api/v1/reference-data/ingredients is called with search="apple"
//   Then "Apple" appears before "Pineapple" regardless of reference counts
//
// Scenario: Search ingredients — ties on reference count fall back to name ascending
//   Given "Apple Cider" and "Apple Juice" both have RecipeReferenceCount = 5
//   When GET /api/v1/reference-data/ingredients is called with search="apple"
//   Then "Apple Cider" appears before "Apple Juice" (alphabetical tiebreak)
//
// Scenario: Search ingredients — surrounding whitespace in search term is trimmed before matching
//   Given a canonical ingredient named "Garlic" exists
//   When GET /api/v1/reference-data/ingredients is called with search="  Garlic  "
//   Then "Garlic" appears in the results
//
// Scenario: Search ingredients — limit exactly 50 is accepted unchanged
//   Given more than 50 matching canonical ingredients exist
//   When GET /api/v1/reference-data/ingredients is called with limit=50
//   Then the response contains exactly 50 items
//
// Scenario: Search ingredients — limit exactly 1 is accepted unchanged
//   Given at least 2 matching canonical ingredients exist
//   When GET /api/v1/reference-data/ingredients is called with limit=1
//   Then the response contains exactly 1 item
//
// Scenario: Search ingredients — response DTO does not expose RecipeReferenceCount
//   Given a canonical ingredient with a non-zero RecipeReferenceCount exists
//   When GET /api/v1/reference-data/ingredients is called with a matching search term
//   Then each item in the response has exactly the fields: Id, Name, Category, DefaultUnitOfMeasureId
//   And RecipeReferenceCount is not present on the DTO
//
// Note: The Npgsql branch of IngredientSearchHelper.ApplySearch (using EF.Functions.ILike)
// is unreachable via the EF Core in-memory provider.  All controller-level search tests
// exercise the in-memory fallback (ToLower().Contains/StartsWith).  EscapeILikeWildcards
// is tested directly in IngredientSearchHelperTests.cs.
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
            DefaultUnitOfMeasureId = Guid.NewGuid(),
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
            DefaultUnitOfMeasureId = Guid.NewGuid(),
            Name = "   "
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    // ── CreateIngredient — unit of measure not found ──────────────────────────────────────

    [Fact]
    public async Task CreateIngredient_UnitOfMeasureNotFound_Returns400BadRequest()
    {
        // Arrange — DefaultUnitOfMeasureId references a unit of measure that does not exist in the DB
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = Guid.NewGuid(),
            Name = "Tomato"
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateIngredient_UnitOfMeasureNotFound_ProblemDetailContainsUnitOfMeasureId()
    {
        // Arrange
        var missingUnitOfMeasureId = Guid.NewGuid();
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = missingUnitOfMeasureId,
            Name = "Tomato"
        };

        // Act
        var result = await _sut.CreateIngredient(request, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain(missingUnitOfMeasureId.ToString());
    }

    // ── CreateIngredient — duplicate name ─────────────────────────────────────

    [Fact]
    public async Task CreateIngredient_DuplicateName_Returns409Conflict()
    {
        // Arrange — seed a unit of measure and an existing ingredient
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);

        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = unitOfMeasure.Id,
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
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = unitOfMeasure.Id,
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
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        var request = new CreateCanonicalIngredientRequest
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = unitOfMeasure.Id,
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

    // ── SearchIngredients — blank / whitespace search returns empty ───────────

    [Fact]
    public async Task SearchIngredients_BlankSearch_Returns200WithEmptyArray()
    {
        // Arrange — seed some ingredients to confirm they are NOT returned
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);
        await SeedIngredientAsync("Butter", unitOfMeasure.Id);

        // Act
        var result = await _sut.SearchIngredients(search: string.Empty, limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchIngredients_NullSearch_Returns200WithEmptyArray()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);

        // Act — null simulates the query parameter being omitted entirely
        var result = await _sut.SearchIngredients(search: null, limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchIngredients_WhitespaceSearch_Returns200WithEmptyArray()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);

        // Act
        var result = await _sut.SearchIngredients(search: "   ", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>()
            .Which.Should().BeEmpty();
    }

    // ── SearchIngredients — limit clamping ────────────────────────────────────

    [Fact]
    public async Task SearchIngredients_DefaultLimit_Returns20Results()
    {
        // Arrange — seed 25 matching ingredients
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        for (var i = 1; i <= 25; i++)
        {
            await SeedIngredientAsync($"Butter Variety {i:D2}", unitOfMeasure.Id);
        }

        // Act — omit limit so the default (20) applies
        var result = await _sut.SearchIngredients(search: "Butter Variety", cancellationToken: CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(20);
    }

    [Fact]
    public async Task SearchIngredients_LimitAbove50_ClampedTo50()
    {
        // Arrange — seed 60 matching ingredients
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        for (var i = 1; i <= 60; i++)
        {
            await SeedIngredientAsync($"Rice Variety {i:D2}", unitOfMeasure.Id);
        }

        // Act — request limit of 500, which should be clamped to 50
        var result = await _sut.SearchIngredients(search: "Rice Variety", limit: 500, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(50);
    }

    [Fact]
    public async Task SearchIngredients_LimitBelowOne_ClampedTo1()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);
        await SeedIngredientAsync("Garlic Powder", unitOfMeasure.Id);

        // Act — limit of -5 should be clamped to 1
        var result = await _sut.SearchIngredients(search: "Garlic", limit: -5, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchIngredients_LimitOfZero_ClampedTo1()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);
        await SeedIngredientAsync("Garlic Powder", unitOfMeasure.Id);

        // Act — limit of 0 should be clamped to 1
        var result = await _sut.SearchIngredients(search: "Garlic", limit: 0, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(1);
    }

    // ── SearchIngredients — case-insensitive matching ─────────────────────────

    [Fact]
    public async Task SearchIngredients_LowercaseSearchMatchesUppercaseName_ReturnsMatch()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Chicken Breast", unitOfMeasure.Id);

        // Act — search with all lowercase
        var result = await _sut.SearchIngredients(search: "chicken breast", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().ContainSingle(i => i.Name == "Chicken Breast");
    }

    [Fact]
    public async Task SearchIngredients_UppercaseSearchMatchesLowercaseName_ReturnsMatch()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("olive oil", unitOfMeasure.Id);

        // Act — search with all uppercase
        var result = await _sut.SearchIngredients(search: "OLIVE OIL", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().ContainSingle(i => i.Name == "olive oil");
    }

    // ── SearchIngredients — ordering: prefix before substring, then name ──────

    [Fact]
    public async Task SearchIngredients_PrefixMatchRankedBeforeSubstringMatch()
    {
        // Arrange — "Garlic" is a prefix match; "Roasted Garlic Hummus" is a substring match
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Roasted Garlic Hummus", unitOfMeasure.Id);
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);

        // Act
        var result = await _sut.SearchIngredients(search: "Garlic", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(2);
        list[0].Name.Should().Be("Garlic");
        list[1].Name.Should().Be("Roasted Garlic Hummus");
    }

    [Fact]
    public async Task SearchIngredients_TiesWithinSameRankTierOrderedByNameAscending()
    {
        // Arrange — both are substring matches (neither is a prefix match for "spinach")
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Wilted Spinach", unitOfMeasure.Id);
        await SeedIngredientAsync("Baby Spinach", unitOfMeasure.Id);
        await SeedIngredientAsync("Creamed Spinach", unitOfMeasure.Id);

        // Act
        var result = await _sut.SearchIngredients(search: "spinach", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var names = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject
            .Select(i => i.Name).ToList();
        names.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task SearchIngredients_PrefixMatchesSortedAlphabeticallyAmongThemselves()
    {
        // Arrange — "Garlic Oil", "Garlic Powder", "Garlic Salt" are all prefix matches
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic Salt", unitOfMeasure.Id);
        await SeedIngredientAsync("Garlic Oil", unitOfMeasure.Id);
        await SeedIngredientAsync("Garlic Powder", unitOfMeasure.Id);

        // Act
        var result = await _sut.SearchIngredients(search: "Garlic", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var names = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject
            .Select(i => i.Name).ToList();
        names.Should().BeInAscendingOrder();
    }

    // ── SearchIngredients — wildcard characters treated literally ─────────────

    [Fact]
    public async Task SearchIngredients_PercentSignInSearchTerm_TreatedAsLiteral()
    {
        // Arrange — only one ingredient whose name literally contains "100%"
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("100% Whole Wheat Flour", unitOfMeasure.Id);
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);
        await SeedIngredientAsync("Butter", unitOfMeasure.Id);

        // Act — "%" must not be treated as an ILike wildcard
        var result = await _sut.SearchIngredients(search: "100%", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().ContainSingle(i => i.Name == "100% Whole Wheat Flour");
    }

    [Fact]
    public async Task SearchIngredients_UnderscoreInSearchTerm_TreatedAsLiteral()
    {
        // Arrange — only one ingredient whose name literally contains "_"
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Secret_Sauce", unitOfMeasure.Id);
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);
        await SeedIngredientAsync("Butter", unitOfMeasure.Id);

        // Act — "_" must not be treated as an ILike single-character wildcard
        var result = await _sut.SearchIngredients(search: "Secret_Sauce", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().ContainSingle(i => i.Name == "Secret_Sauce");
    }

    // ── SearchIngredients — DTO projection ────────────────────────────────────

    [Fact]
    public async Task SearchIngredients_MatchingIngredient_ResponseContainsDtoFields()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        var seeded = await SeedIngredientAsync("Olive Oil", unitOfMeasure.Id);

        // Act
        var result = await _sut.SearchIngredients(search: "Olive Oil", limit: 20, CancellationToken.None);

        // Assert — verify that all DTO fields are present and correct
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().ContainSingle();
        var dto = list[0];
        dto.Id.Should().Be(seeded.Id);
        dto.Name.Should().Be("Olive Oil");
        dto.Category.Should().Be(IngredientCategory.Produce);
        dto.DefaultUnitOfMeasureId.Should().Be(unitOfMeasure.Id);
    }

    [Fact]
    public async Task SearchIngredients_NoMatchingIngredients_Returns200WithEmptyArray()
    {
        // Arrange — seed an ingredient that does not match the search term
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Avocado", unitOfMeasure.Id);

        // Act
        var result = await _sut.SearchIngredients(search: "zucchini", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>()
            .Which.Should().BeEmpty();
    }

    // ── SearchIngredients — recipe-usage ranking ──────────────────────────────

    [Fact]
    public async Task SearchIngredients_WithinPrefixMatches_MoreReferencedIngredientSortsFirst()
    {
        // Arrange — "Apple" has RecipeReferenceCount = 10; "Apple Cider" has RecipeReferenceCount = 2
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Apple", unitOfMeasure.Id, recipeReferenceCount: 10);
        await SeedIngredientAsync("Apple Cider", unitOfMeasure.Id, recipeReferenceCount: 2);

        // Act
        var result = await _sut.SearchIngredients(search: "Apple", limit: 20, CancellationToken.None);

        // Assert — both are prefix matches; the more-referenced one must appear first
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(2);
        list[0].Name.Should().Be("Apple");
        list[1].Name.Should().Be("Apple Cider");
    }

    [Fact]
    public async Task SearchIngredients_PrefixMatchWithZeroReferences_StillRanksAboveContainsMatchWithManyReferences()
    {
        // Arrange — "Apple" is a prefix match with RecipeReferenceCount = 0;
        //           "Pineapple" is a contains match with RecipeReferenceCount = 50
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Apple", unitOfMeasure.Id, recipeReferenceCount: 0);
        await SeedIngredientAsync("Pineapple", unitOfMeasure.Id, recipeReferenceCount: 50);

        // Act
        var result = await _sut.SearchIngredients(search: "apple", limit: 20, CancellationToken.None);

        // Assert — prefix rank takes precedence over reference count
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(2);
        list[0].Name.Should().Be("Apple");
        list[1].Name.Should().Be("Pineapple");
    }

    [Fact]
    public async Task SearchIngredients_TiesOnReferenceCount_FallBackToNameAscending()
    {
        // Arrange — "Apple Cider" and "Apple Juice" are both prefix matches with RecipeReferenceCount = 5
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Apple Cider", unitOfMeasure.Id, recipeReferenceCount: 5);
        await SeedIngredientAsync("Apple Juice", unitOfMeasure.Id, recipeReferenceCount: 5);

        // Act
        var result = await _sut.SearchIngredients(search: "apple", limit: 20, CancellationToken.None);

        // Assert — name ascending is the tiebreaker; "Apple Cider" < "Apple Juice"
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(2);
        list[0].Name.Should().Be("Apple Cider");
        list[1].Name.Should().Be("Apple Juice");
    }

    // ── SearchIngredients — whitespace trimming ───────────────────────────────

    [Fact]
    public async Task SearchIngredients_SurroundingWhitespaceInSearchTerm_TrimmedBeforeMatching()
    {
        // Arrange — ingredient name has no leading/trailing spaces; search term does
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Garlic", unitOfMeasure.Id);

        // Act — controller calls search.Trim() before forwarding to ApplySearch
        var result = await _sut.SearchIngredients(search: "  Garlic  ", limit: 20, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().ContainSingle(i => i.Name == "Garlic");
    }

    // ── SearchIngredients — boundary limit values ─────────────────────────────

    [Fact]
    public async Task SearchIngredients_LimitExactly50_AcceptedUnchangedReturns50Results()
    {
        // Arrange — seed exactly 55 matching ingredients so we can observe the exact 50 returned
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        for (var i = 1; i <= 55; i++)
        {
            await SeedIngredientAsync($"Wheat Variety {i:D2}", unitOfMeasure.Id);
        }

        // Act — limit of exactly 50 must not be clamped further
        var result = await _sut.SearchIngredients(search: "Wheat Variety", limit: 50, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(50);
    }

    [Fact]
    public async Task SearchIngredients_LimitExactly1_AcceptedUnchangedReturns1Result()
    {
        // Arrange
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Salt", unitOfMeasure.Id);
        await SeedIngredientAsync("Sea Salt", unitOfMeasure.Id);
        await SeedIngredientAsync("Smoked Salt", unitOfMeasure.Id);

        // Act — limit of exactly 1 must not be clamped to a higher value
        var result = await _sut.SearchIngredients(search: "Salt", limit: 1, CancellationToken.None);

        // Assert
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().HaveCount(1);
    }

    // ── SearchIngredients — DTO field containment ─────────────────────────────

    [Fact]
    public async Task SearchIngredients_MatchingIngredient_DtoDoesNotExposeRecipeReferenceCount()
    {
        // Arrange — seed an ingredient with a non-zero RecipeReferenceCount to confirm
        // it is not surfaced through the CanonicalIngredientDto projection
        var unitOfMeasure = await SeedUnitOfMeasureAsync();
        await SeedIngredientAsync("Parsley", unitOfMeasure.Id, recipeReferenceCount: 9999);

        // Act
        var result = await _sut.SearchIngredients(search: "Parsley", limit: 20, CancellationToken.None);

        // Assert — the DTO type itself must not declare a RecipeReferenceCount property;
        // this guards against accidental field additions that would expose the internal
        // ranking data to callers.
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IReadOnlyList<CanonicalIngredientDto>>().Subject;
        list.Should().ContainSingle();

        var dtoType = typeof(CanonicalIngredientDto);
        dtoType.GetProperty("RecipeReferenceCount").Should().BeNull(
            because: "RecipeReferenceCount is an internal ranking signal and must not appear on the public DTO");
    }

    // ── ListUnits ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListUnits_UnitsExist_Returns200WithOrderedList()
    {
        // Arrange
        await SeedUnitOfMeasureWithNameAsync("Teaspoon");
        await SeedUnitOfMeasureWithNameAsync("Cup");
        await SeedUnitOfMeasureWithNameAsync("Gram");

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

    private async Task<CanonicalIngredient> SeedIngredientAsync(
        string name,
        Guid unitOfMeasureId,
        int recipeReferenceCount = 0)
    {
        var ingredient = new CanonicalIngredient
        {
            Category = IngredientCategory.Produce,
            DefaultUnitOfMeasureId = unitOfMeasureId,
            Id = Guid.NewGuid(),
            Name = name,
            RecipeReferenceCount = recipeReferenceCount
        };
        _dbContext.CanonicalIngredients.Add(ingredient);
        await _dbContext.SaveChangesAsync();
        return ingredient;
    }

    private async Task<UnitOfMeasure> SeedUnitOfMeasureAsync()
    {
        return await SeedUnitOfMeasureWithNameAsync("gram");
    }

    private async Task<UnitOfMeasure> SeedUnitOfMeasureWithNameAsync(string name)
    {
        var unitOfMeasure = new UnitOfMeasure
        {
            Abbreviation = name[..1].ToLowerInvariant(),
            ConversionFactor = 1.0m,
            Id = Guid.NewGuid(),
            Name = name,
            UnitOfMeasureType = UnitOfMeasureType.Weight
        };
        _dbContext.UnitsOfMeasure.Add(unitOfMeasure);
        await _dbContext.SaveChangesAsync();
        return unitOfMeasure;
    }
}
