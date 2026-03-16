using System.Text.Json.Serialization;
using MealsEnPlace.Api.Common;
using MealsEnPlace.Api.Features.Inventory;
using MealsEnPlace.Api.Features.Recipes;
using MealsEnPlace.Api.Infrastructure.Claude;
using MealsEnPlace.Api.Infrastructure.Data;
using MealsEnPlace.Api.Infrastructure.ExternalApis.TheMealDb;
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

// -- Application services -----------------------------------------------------
builder.Services.AddHttpClient("TheMealDb", client =>
{
    client.BaseAddress = new Uri("https://www.themealdb.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddScoped<IClaudeService, ClaudeService>();
builder.Services.AddScoped<IContainerResolutionService, ContainerResolutionService>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IRecipeImportService, RecipeImportService>();
builder.Services.AddScoped<IRecipeMatchingService, RecipeMatchingService>();
builder.Services.AddScoped<ITheMealDbClient, TheMealDbClient>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IUomConversionService, UomConversionService>();
builder.Services.AddScoped<IUomNormalizationService, UomNormalizationService>();
builder.Services.AddScoped<UomDisplayConverter>();

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
