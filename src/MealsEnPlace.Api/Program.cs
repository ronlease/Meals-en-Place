using System.Text.Json.Serialization;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Inventory;
using MealsEnPlace.Api.Features.MealPlan;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Features.SeasonalProduce;
using MealsEnPlace.Api.Features.Settings;
using MealsEnPlace.Api.Features.ShoppingList;
using MealsEnPlace.Api.Features.WasteReduction;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// -- Database -----------------------------------------------------------------
builder.Services.AddDbContext<MealsEnPlaceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// -- Controllers & JSON -------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// -- Swagger / OpenAPI --------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Description = "Meals en Place API - inventory, recipe matching, meal planning, and waste reduction.",
        Title = "Meals en Place API",
        Version = "v1"
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// -- CORS ---------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4280", "https://localhost:4280")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// -- BYO Claude API key storage -----------------------------------------------
// Encrypt the user-supplied Anthropic token at rest via ASP.NET DataProtection.
// Keys and token file live under LocalApplicationData so they never land in the
// repo and persist across app restarts.
var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var settingsDirectory = Path.Combine(localAppData, "MealsEnPlace");
var keyRingDirectory = Path.Combine(settingsDirectory, "keys");
var tokenFilePath = Path.Combine(settingsDirectory, "claude-token.dat");
Directory.CreateDirectory(keyRingDirectory);

builder.Services.AddDataProtection()
    .SetApplicationName("MealsEnPlace")
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingDirectory));

builder.Services.AddSingleton(new ClaudeTokenStoreOptions
{
    KeyRingDirectory = keyRingDirectory,
    TokenFilePath = tokenFilePath
});
builder.Services.AddSingleton<IClaudeTokenStore, ClaudeTokenStore>();
builder.Services.AddScoped<IClaudeAvailability, ClaudeAvailability>();

// -- Application services -----------------------------------------------------
builder.Services.AddHttpClient("Anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IAnthropicTestClient, AnthropicTestClient>();
builder.Services.AddScoped<IClaudeService, ClaudeService>();
builder.Services.AddScoped<IContainerResolutionService, ContainerResolutionService>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IMealConsumptionService, MealConsumptionService>();
builder.Services.AddScoped<IMealPlanReorderService, MealPlanReorderService>();
builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddScoped<IRecipeImportService, RecipeImportService>();
builder.Services.AddScoped<IRecipeMatchingService, RecipeMatchingService>();
builder.Services.AddScoped<ISeasonalProduceService, SeasonalProduceService>();
builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IUnitOfMeasureConversionService, UnitOfMeasureConversionService>();
builder.Services.AddScoped<IUnitOfMeasureNormalizationService, UnitOfMeasureNormalizationService>();
builder.Services.AddScoped<IWasteAlertService, WasteAlertService>();
builder.Services.AddScoped<UnitOfMeasureDisplayConverter>();

// -- Problem details ----------------------------------------------------------
builder.Services.AddProblemDetails();

var app = builder.Build();

// -- HTTP pipeline ------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Meals en Place API v1"));
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();

// Exposes the generated Program class to WebApplicationFactory<Program> in integration tests.
public partial class Program { }
