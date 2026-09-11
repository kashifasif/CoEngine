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

builder.Services.AddScoped<CopilotAiService>();
builder.Services.AddScoped<ICopilotAiService>(sp => sp.GetRequiredService<CopilotAiService>());
builder.Services.AddScoped<IOpenRouterService>(sp => sp.GetRequiredService<CopilotAiService>());

// Configure Semantic Kernel: Optional upstream LLM endpoint (if explicitly configured on server)
var kernelBuilder = builder.Services.AddKernel();
var copilotBridgeUrl = builder.Configuration["CopilotBridge:Endpoint"] 
    ?? builder.Configuration["Copilot:Endpoint"] 
    ?? Environment.GetEnvironmentVariable("COPILOT_BRIDGE_URL");

if (!string.IsNullOrWhiteSpace(copilotBridgeUrl))
{
    var copilotModel = builder.Configuration["CopilotBridge:ModelId"] ?? "gpt-4o";
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: copilotModel,
        apiKey: "copilot-bridge",
        endpoint: new Uri(copilotBridgeUrl.TrimEnd('/') + "/"));
}

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
