using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CoEngine.Api.Data;
using CoEngine.Shared.DTOs;
using CoEngine.Shared.Models;

namespace CoEngine.Api.Services;

public class GitHubAuthService : IGitHubAuthService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubAuthService> _logger;

    public GitHubAuthService(
        HttpClient httpClient,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<GitHubAuthService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string redirectUri, string? state = null)
    {
        var clientId = _configuration["GitHub:ClientId"] ?? "dev_github_client_id";
        var stateParam = string.IsNullOrWhiteSpace(state) ? Guid.NewGuid().ToString("N") : state;

        return $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=user:email&state={Uri.EscapeDataString(stateParam)}";
    }

    public async Task<UserDto> ProcessCallbackAsync(string code, string? redirectUri, CancellationToken cancellationToken = default)
    {
        var clientId = _configuration["GitHub:ClientId"] ?? "";
        var clientSecret = _configuration["GitHub:ClientSecret"] ?? "";

        // Dev / Test Mock Fallback Mode
        if (code.StartsWith("mock_") || code.StartsWith("test_") || string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogInformation("Using Dev/Test GitHub Mock Auth for code: {Code}", code);
            var mockGitHubId = "dev_gh_12345";
            var mockUsername = "github_developer";
            var mockEmail = "developer@coengine.io";
            var mockAvatar = "https://github.com/identicons/coengine.png";

            return await UpsertUserAndMapDtoAsync(mockGitHubId, mockUsername, "GitHub Developer", mockEmail, mockAvatar, cancellationToken);
        }

        // Live GitHub OAuth Token Exchange
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        tokenRequest.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri ?? "")
        });

        var tokenResponse = await _httpClient.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var accessToken = tokenJson.TryGetProperty("access_token", out var accessTokenProp) ? accessTokenProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Failed to obtain access token from GitHub.");
        }

        // Fetch User Profile from GitHub API
        var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        userRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("CoEngine-App", "1.0"));

        var userResponse = await _httpClient.SendAsync(userRequest, cancellationToken);
        userResponse.EnsureSuccessStatusCode();

        var userJson = await userResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var ghId = userJson.GetProperty("id").GetInt64().ToString();
        var username = userJson.GetProperty("login").GetString() ?? "github_user";
        var displayName = userJson.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? username : username;
        var avatarUrl = userJson.TryGetProperty("avatar_url", out var avatarProp) ? avatarProp.GetString() ?? "" : "";
        var email = userJson.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;

        // Fetch primary email if null/private
        if (string.IsNullOrWhiteSpace(email))
        {
            try
            {
                var emailsRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                emailsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                emailsRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("CoEngine-App", "1.0"));

                var emailsResponse = await _httpClient.SendAsync(emailsRequest, cancellationToken);
                if (emailsResponse.IsSuccessStatusCode)
                {
                    var emailsJson = await emailsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                    foreach (var em in emailsJson.EnumerateArray())
                    {
                        if (em.TryGetProperty("primary", out var primProp) && primProp.GetBoolean())
                        {
                            email = em.GetProperty("email").GetString();
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        email ??= $"{username}@users.noreply.github.com";

        return await UpsertUserAndMapDtoAsync(ghId, username, displayName, email, avatarUrl, cancellationToken);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null) return null;

        return MapToUserDto(user);
    }

    private async Task<UserDto> UpsertUserAndMapDtoAsync(
        string gitHubId,
        string username,
        string displayName,
        string email,
        string avatarUrl,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == gitHubId, cancellationToken);
        if (user == null)
        {
            user = new User
            {
                GitHubId = gitHubId,
                Username = username,
                DisplayName = displayName,
                Email = email,
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Username = username;
            user.DisplayName = displayName;
            user.Email = email;
            user.AvatarUrl = avatarUrl;
            user.LastLoginAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return MapToUserDto(user);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            GitHubId = user.GitHubId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Token = $"gh_session_{user.Id}_{Guid.NewGuid():N}",
            IsAuthenticated = true
        };
    }
}
