using Microsoft.EntityFrameworkCore;
using SpecPlatform.Api.Data;
using SpecPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with Global Authorization Protection
builder.Services.AddControllers(options =>
{
    options.Filters.Add<SpecPlatform.Api.Infrastructure.RequireAuthorizationFilter>();
});
builder.Services.AddEndpointsApiExplorer();

// Add EF Core DbContext — PostgreSQL only
var connectionString = builder.Configuration.GetConnectionString("PostgresConnection")
    ?? throw new InvalidOperationException("PostgresConnection string is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));


// Add OpenRouter AI Service, GitHub Auth Service & PostgreSQL pgvector Service
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>();
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

// Ensure DB Created & Schema Migration for pgvector and Users
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

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

            CREATE TABLE IF NOT EXISTS ""VectorDocuments"" (
                ""Id"" TEXT PRIMARY KEY,
                ""ProjectId"" INTEGER NOT NULL,
                ""DocType"" TEXT NOT NULL,
                ""Title"" TEXT NOT NULL,
                ""Content"" TEXT NOT NULL,
                ""Embedding"" vector(128),
                ""IndexedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS ""IX_VectorDocuments_ProjectId"" ON ""VectorDocuments"" (""ProjectId"");
        ");
    }
    catch { }
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowAll");

// Serve Blazor WebAssembly static framework files and fallback route
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
