using CoEngine.Shared.DTOs;

namespace CoEngine.Api.Services;

public interface IGitHubAuthService
{
    string GetAuthorizationUrl(string redirectUri, string? state = null);
    Task<UserDto> ProcessCallbackAsync(string code, string? redirectUri, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
