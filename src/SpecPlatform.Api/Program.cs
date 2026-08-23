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
    options.UseNpgsql(connectionString));


// Add OpenRouter AI Service, GitHub Auth Service & Local Vector DB Service
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>();
builder.Services.AddHttpClient<IGitHubAuthService, GitHubAuthService>();
builder.Services.AddSingleton<IVectorStoreService, LocalVectorStoreService>();

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

// Ensure DB Created & Schema Migration for ChatSessions
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();
    db.Database.EnsureCreated();

    // Ensure PostgreSQL Users table exists
    try
    {
        db.Database.ExecuteSqlRaw(@"
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
        ");
    }
    catch { }

    // Pre-index all specifications from SQLite into Vector Store on server startup
    try
    {
        var allSpecs = db.Specs
            .Include(s => s.Versions).ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions).ThenInclude(v => v.ScopeTags)
            .ToList();

        foreach (var spec in allSpecs)
        {
            var latestVer = spec.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            var criteriaText = string.Join(". ", latestVer?.AcceptanceCriteria.Select(a => a.Text) ?? Array.Empty<string>());
            var tagsText = string.Join(", ", latestVer?.ScopeTags.Select(t => t.TagName) ?? Array.Empty<string>());
            vectorStore.IndexDocumentAsync(spec.ProjectId, "spec", spec.Title, $"{spec.Description}. Acceptance criteria: {criteriaText}. Scope tags: {tagsText}").GetAwaiter().GetResult();
        }
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
