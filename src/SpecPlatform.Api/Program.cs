using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SpecPlatform.Api.Data;
using SpecPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Native Session Authentication & Authorization
builder.Services.AddAuthentication("SessionAuth")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SpecPlatform.Api.Infrastructure.SessionAuthHandler>("SessionAuth", null);

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

// Add CORS Policy for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply all pending EF Core migrations automatically on startup (dev + prod).
// MigrateAsync() is idempotent — it checks __EFMigrationsHistory and skips already-applied migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Ensure PostgreSQL pgvector extension and tables exist
    try
    {
            db.Database.ExecuteSqlRaw(@"
                CREATE EXTENSION IF NOT EXISTS vector;

                CREATE TABLE IF NOT EXISTS ""Users"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""GitHubId"" TEXT NOT NULL DEFAULT '',
                    ""Username"" TEXT NOT NULL DEFAULT '',
                    ""DisplayName"" TEXT NOT NULL DEFAULT '',
                    ""Email"" TEXT NOT NULL DEFAULT '',
                    ""AvatarUrl"" TEXT NOT NULL DEFAULT '',
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""LastLoginAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                DROP TABLE IF EXISTS ""VectorDocuments"";
                CREATE TABLE ""VectorDocuments"" (
                    ""Id"" TEXT PRIMARY KEY,
                    ""ProjectId"" INTEGER NOT NULL,
                    ""DocType"" TEXT NOT NULL,
                    ""Title"" TEXT NOT NULL,
                    ""Content"" TEXT NOT NULL,
                    ""Embedding"" vector,
                    ""IndexedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX ""IX_VectorDocuments_ProjectId"" ON ""VectorDocuments"" (""ProjectId"");
            ");

        // Backfill any existing notifications created before author tracking with real user info
        var defaultUser = await db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
        if (defaultUser != null)
        {
            var unassignedNotifs = await db.Notifications
                .Where(n => n.AuthorUserId == null)
                .ToListAsync();

            if (unassignedNotifs.Any())
            {
                foreach (var notif in unassignedNotifs)
                {
                    notif.AuthorUserId = defaultUser.Id;
                }
                await db.SaveChangesAsync();
            }

            var unassignedVersions = await db.SpecVersions
                .Where(v => v.AuthorUserId == null)
                .ToListAsync();

            if (unassignedVersions.Any())
            {
                foreach (var version in unassignedVersions)
                {
                    version.AuthorUserId = defaultUser.Id;
                }
                await db.SaveChangesAsync();
            }
        }
    }
    catch { }
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
