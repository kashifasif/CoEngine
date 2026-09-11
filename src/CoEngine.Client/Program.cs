using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CoEngine.Client;
using CoEngine.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddTransient<CookieHandler>();
builder.Services.AddScoped<CopilotAiClient>();

builder.Services.AddHttpClient<SpecApiClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiBase = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
    if (string.IsNullOrWhiteSpace(apiBase) || apiBase.Contains(":5123"))
    {
        apiBase = "http://localhost:5005";
    }
    client.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
}).AddHttpMessageHandler<CookieHandler>();

await builder.Build().RunAsync();
