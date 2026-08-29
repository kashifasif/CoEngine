using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoEngine.Api.Services;
using CoEngine.Shared.DTOs;

namespace CoEngine.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IGitHubAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IGitHubAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    /// <summary>
    /// Builds the OAuth redirect URI from configuration.
    /// GitHub:AppBaseUrl + GitHub:CallbackPath (e.g. http://localhost:5005 + /auth/github/callback)
    /// </summary>
    private string GetCallbackRedirectUri()
    {
        var baseUrl = _configuration["GitHub:AppBaseUrl"]?.TrimEnd('/');
        var callbackPath = _configuration["GitHub:CallbackPath"] ?? "/auth/github/callback";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // Fallback: derive from incoming request host (safe for local dev if config missing)
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        }

        return $"{baseUrl}{callbackPath}";
    }

    [HttpGet("github/url")]
    public ActionResult<GitHubAuthUrlDto> GetGitHubAuthUrl([FromQuery] string? state = null)
    {
        var redirectUri = GetCallbackRedirectUri();
        var url = _authService.GetAuthorizationUrl(redirectUri, state);
        return Ok(new GitHubAuthUrlDto { AuthUrl = url });
    }

    [HttpPost("github/callback")]
    public async Task<ActionResult<UserDto>> ProcessCallback(
        [FromBody] GitHubCallbackRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "OAuth code is required." });
        }

        var redirectUri = GetCallbackRedirectUri();
        var userDto = await _authService.ProcessCallbackAsync(request.Code, redirectUri, cancellationToken);
        if (userDto != null && !string.IsNullOrWhiteSpace(userDto.Token))
        {
            Response.Cookies.Append("spec_user_session", userDto.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            // We can erase the token from the DTO payload for extra security since it's in the cookie
            userDto.Token = string.Empty;
        }
        return Ok(userDto);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser(
        [FromQuery] Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            var user = await _authService.GetUserByIdAsync(userId.Value, cancellationToken);
            if (user != null)
            {
                user.IsAuthenticated = true;
                return Ok(user);
            }
        }

        var token = Request.Cookies["coengine_session"] 
            ?? Request.Cookies["spec_user_session"] 
            ?? Request.Headers["X-Auth-Token"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
        {
            return Ok(new UserDto
            {
                Id = Guid.Empty,
                Username = "Guest User",
                DisplayName = "Guest User",
                IsAuthenticated = false
            });
        }

        // Token format: gh_session_{userId}_{guid}
        if (token.StartsWith("gh_session_", StringComparison.OrdinalIgnoreCase) || token.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = token.Split('_');
            if (parts.Length >= 3 && Guid.TryParse(parts[2], out var tokenUserId))
            {
                var user = await _authService.GetUserByIdAsync(tokenUserId, cancellationToken);
                if (user != null)
                {
                    user.IsAuthenticated = true;
                    return Ok(user);
                }
            }
        }

        return Ok(new UserDto
        {
            Id = Guid.Empty,
            Username = "Guest User",
            DisplayName = "Guest User",
            IsAuthenticated = false
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("spec_user_session");
        return Ok(new { success = true, message = "Logged out successfully." });
    }
}
