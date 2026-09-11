using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace CoEngine.Client.Services;

public class CookieHandler : DelegatingHandler
{
    private readonly IJSRuntime _js;

    public CookieHandler(IJSRuntime js)
    {
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, "spec_user_session");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.TryAddWithoutValidation("X-Session-Token", token);
            }

            var storedApiUrl = await _js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, "coengine_api_url");
            if (!string.IsNullOrWhiteSpace(storedApiUrl) && request.RequestUri != null)
            {
                if (request.RequestUri.Host == "127.0.0.1" && request.RequestUri.Port == 5123)
                {
                    var targetBase = new Uri(storedApiUrl.TrimEnd('/') + "/");
                    request.RequestUri = new Uri(targetBase, request.RequestUri.PathAndQuery.TrimStart('/'));
                }
            }
        }
        catch
        {
            // Ignore if JS interop is not yet initialized
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
