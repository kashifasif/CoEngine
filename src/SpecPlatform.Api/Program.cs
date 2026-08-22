using Microsoft.EntityFrameworkCore;
using SpecPlatform.Api.Data;
using SpecPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add SQLite EF Core DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=specplatform.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Add OpenRouter AI Service & Local Vector DB Service
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>();
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
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""ChatSessions"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ChatSessions"" PRIMARY KEY AUTOINCREMENT,
                ""ProjectId"" INTEGER NOT NULL,
                ""PersonaMode"" TEXT NOT NULL,
                ""CreatedAt"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT NOT NULL,
                CONSTRAINT ""FK_ChatSessions_Projects_ProjectId"" FOREIGN KEY (""ProjectId"") REFERENCES ""Projects"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""ChatMessages"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ChatMessages"" PRIMARY KEY AUTOINCREMENT,
                ""ChatSessionId"" INTEGER NOT NULL,
                ""Role"" TEXT NOT NULL,
                ""Content"" TEXT NOT NULL,
                ""Timestamp"" TEXT NOT NULL,
                CONSTRAINT ""FK_ChatMessages_ChatSessions_ChatSessionId"" FOREIGN KEY (""ChatSessionId"") REFERENCES ""ChatSessions"" (""Id"") ON DELETE CASCADE
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

// Serve Blazor WebAssembly static framework files and fallback route
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
