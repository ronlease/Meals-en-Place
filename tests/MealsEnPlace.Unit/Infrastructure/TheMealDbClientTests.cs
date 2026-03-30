// Feature: TheMealDbClient
//
// Scenario: SearchByNameAsync returns meals when API responds with results
//   Given the HTTP response contains a valid meals array
//   When SearchByNameAsync is called with a valid query
//   Then the returned list contains the expected meals
//
// Scenario: SearchByNameAsync returns empty list when API throws exception
//   Given the HTTP client throws an exception
//   When SearchByNameAsync is called
//   Then an empty list is returned without throwing
//
// Scenario: GetByIdAsync returns meal when API responds with a result
//   Given the HTTP response contains a valid meal in the array
//   When GetByIdAsync is called with a valid ID
//   Then the meal is returned
//
// Scenario: GetByIdAsync returns null when API throws exception
//   Given the HTTP client throws an exception
//   When GetByIdAsync is called
//   Then null is returned without throwing
//
// Scenario: FilterByCategoryAsync returns meals when API responds with results
//   Given the HTTP response contains a valid meals array
//   When FilterByCategoryAsync is called with a valid category
//   Then the returned list contains the expected meals
//
// Scenario: FilterByCategoryAsync returns empty list when API throws exception
//   Given the HTTP client throws an exception
//   When FilterByCategoryAsync is called
//   Then an empty list is returned without throwing

using System.Net;
using System.Text.Json;
using FluentAssertions;
using MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace MealsEnPlace.Unit.Infrastructure;

public class TheMealDbClientTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new(MockBehavior.Strict);
    private readonly Mock<ILogger<TheMealDbClient>> _loggerMock = new();

    // ── SearchByNameAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SearchByNameAsync_ValidResponse_ReturnsMeals()
    {
        // Arrange
        var meals = new[] { BuildMeal("52772", "Chicken Tikka Masala") };
        var client = BuildHttpClientWithResponse(BuildJsonResponse(meals));
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.SearchByNameAsync("chicken", CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].MealId.Should().Be("52772");
        result[0].MealName.Should().Be("Chicken Tikka Masala");
    }

    [Fact]
    public async Task SearchByNameAsync_ApiReturnsNullMeals_ReturnsEmptyList()
    {
        // Arrange
        var client = BuildHttpClientWithResponse("""{"meals":null}""");
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.SearchByNameAsync("nosuchquery", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByNameAsync_HttpClientThrowsException_ReturnsEmptyList()
    {
        // Arrange
        var client = BuildFaultingHttpClient();
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.SearchByNameAsync("chicken", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByNameAsync_HttpClientThrowsException_DoesNotPropagateException()
    {
        // Arrange
        var client = BuildFaultingHttpClient();
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var act = async () => await sut.SearchByNameAsync("chicken", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ValidResponse_ReturnsMeal()
    {
        // Arrange
        var meals = new[] { BuildMeal("52772", "Chicken Tikka Masala") };
        var client = BuildHttpClientWithResponse(BuildJsonResponse(meals));
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.GetByIdAsync("52772", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.MealId.Should().Be("52772");
    }

    [Fact]
    public async Task GetByIdAsync_ApiReturnsNullMeals_ReturnsNull()
    {
        // Arrange
        var client = BuildHttpClientWithResponse("""{"meals":null}""");
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.GetByIdAsync("99999", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_HttpClientThrowsException_ReturnsNull()
    {
        // Arrange
        var client = BuildFaultingHttpClient();
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.GetByIdAsync("52772", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_HttpClientThrowsException_DoesNotPropagateException()
    {
        // Arrange
        var client = BuildFaultingHttpClient();
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var act = async () => await sut.GetByIdAsync("52772", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── FilterByCategoryAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task FilterByCategoryAsync_ValidResponse_ReturnsMeals()
    {
        // Arrange
        var meals = new[]
        {
            BuildMeal("52772", "Chicken Tikka Masala"),
            BuildMeal("52959", "Baked Salmon with Fennel")
        };
        var client = BuildHttpClientWithResponse(BuildJsonResponse(meals));
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.FilterByCategoryAsync("Chicken", CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FilterByCategoryAsync_ApiReturnsNullMeals_ReturnsEmptyList()
    {
        // Arrange
        var client = BuildHttpClientWithResponse("""{"meals":null}""");
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.FilterByCategoryAsync("Unknown", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterByCategoryAsync_HttpClientThrowsException_ReturnsEmptyList()
    {
        // Arrange
        var client = BuildFaultingHttpClient();
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.FilterByCategoryAsync("Chicken", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterByCategoryAsync_HttpClientThrowsException_DoesNotPropagateException()
    {
        // Arrange
        var client = BuildFaultingHttpClient();
        _httpClientFactoryMock.Setup(f => f.CreateClient("TheMealDb")).Returns(client);
        var sut = new TheMealDbClient(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var act = async () => await sut.FilterByCategoryAsync("Chicken", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static string BuildJsonResponse(object[] meals)
    {
        var wrapper = new { meals };
        return JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null
        });
    }

    private static HttpClient BuildFaultingHttpClient()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network failure"));

        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://www.themealdb.com") };
    }

    private static HttpClient BuildHttpClientWithResponse(string json)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                StatusCode = HttpStatusCode.OK
            });

        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://www.themealdb.com") };
    }

    private static object BuildMeal(string id, string name) =>
        new
        {
            idMeal = id,
            strMeal = name,
            strCategory = "Chicken",
            strArea = "Indian",
            strInstructions = "Cook it.",
            strMealThumb = (string?)null,
            strSource = (string?)null
        };
}
