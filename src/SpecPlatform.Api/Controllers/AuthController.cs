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

    public AuthController(IGitHubAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("github/url")]
    public ActionResult<GitHubAuthUrlDto> GetGitHubAuthUrl([FromQuery] string? redirectUri = null, [FromQuery] string? state = null)
    {
        var effectiveRedirectUri = string.IsNullOrWhiteSpace(redirectUri)
            ? $"{Request.Scheme}://{Request.Host}/auth/github/callback"
            : redirectUri;

        var url = _authService.GetAuthorizationUrl(effectiveRedirectUri, state);
        return Ok(new GitHubAuthUrlDto { AuthUrl = url });
    }

    [HttpPost("github/callback")]
    public async Task<ActionResult<UserDto>> ProcessCallback(
        [FromBody] GitHubCallbackRequestDto request,
        [FromQuery] string? redirectUri = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "OAuth code is required." });
        }

        var effectiveRedirectUri = string.IsNullOrWhiteSpace(redirectUri)
            ? $"{Request.Scheme}://{Request.Host}/auth/github/callback"
            : redirectUri;

        var userDto = await _authService.ProcessCallbackAsync(request.Code, effectiveRedirectUri, cancellationToken);
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
