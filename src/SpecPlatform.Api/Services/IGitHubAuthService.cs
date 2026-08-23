using SpecPlatform.Shared.DTOs;

namespace SpecPlatform.Api.Services;

public interface IGitHubAuthService
{
    string GetAuthorizationUrl(string redirectUri, string? state = null);
    Task<UserDto> ProcessCallbackAsync(string code, string? redirectUri, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);
}
