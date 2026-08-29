using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using CoEngine.Api.Data;
using CoEngine.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Native Session Authentication & Authorization
builder.Services.AddAuthentication("SessionAuth")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, CoEngine.Api.Infrastructure.SessionAuthHandler>("SessionAuth", null);

builder.Services.AddAuthorization();

// Add EF Core DbContext — PostgreSQL only
var connectionString = builder.Configuration.GetConnectionString("PostgresConnection")
    ?? throw new InvalidOperationException("PostgresConnection string is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

// Add OpenRouter AI Service, GitHub Auth Service & PostgreSQL pgvector Service
builder.Services.AddScoped<IOpenRouterService, OpenRouterService>();

// Configure Semantic Kernel with DeepSeek API
var deepSeekApiKey = builder.Configuration["DeepSeek:ApiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "sk-cccd16c9552348cda60e0ed362840130";
var deepSeekModel = builder.Configuration["DeepSeek:Model"] ?? "deepseek-v4-pro";
var deepSeekBaseUrl = builder.Configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com/chat/completions";

// Extract base URL for the OpenAI connector (it typically expects the base like https://api.deepseek.com/v1/)
var baseUri = new Uri(deepSeekBaseUrl);
var deepSeekEndpoint = new Uri(baseUri.GetLeftPart(UriPartial.Authority) + "/v1"); // typically /v1 for OpenAI compatibility

builder.Services.AddKernel()
    .AddOpenAIChatCompletion(
        modelId: deepSeekModel,
        apiKey: deepSeekApiKey,
        endpoint: deepSeekEndpoint);

builder.Services.AddHttpClient<IGitHubAuthService, GitHubAuthService>();
builder.Services.AddScoped<IVectorStoreService, PgVectorStoreService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Add CORS Policy for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Apply all pending EF Core migrations automatically on startup (dev + prod).
// MigrateAsync() is idempotent — it checks __EFMigrationsHistory and skips already-applied migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}


app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Serve Blazor WebAssembly static framework files and fallback route
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
