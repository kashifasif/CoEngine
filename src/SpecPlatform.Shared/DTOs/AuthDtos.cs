namespace SpecPlatform.Shared.DTOs;

public class GitHubAuthUrlDto
{
    public string AuthUrl { get; set; } = string.Empty;
}

public class GitHubCallbackRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string GitHubId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; } = true;
}
