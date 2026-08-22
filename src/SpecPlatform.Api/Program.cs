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
}

// Serve Blazor WebAssembly static framework files and fallback route
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
