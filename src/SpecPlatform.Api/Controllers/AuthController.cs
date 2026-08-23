using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpecPlatform.Api.Services;
using SpecPlatform.Shared.DTOs;

namespace SpecPlatform.Api.Controllers;

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
        return Ok(userDto);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser([FromQuery] int userId = 1, CancellationToken cancellationToken = default)
    {
        var user = await _authService.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return Ok(new UserDto
            {
                Id = 0,
                Username = "Guest User",
                DisplayName = "Guest User",
                IsAuthenticated = false
            });
        }
        return Ok(user);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { success = true, message = "Logged out successfully." });
    }
}
