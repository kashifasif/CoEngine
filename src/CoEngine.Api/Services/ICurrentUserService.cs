using SpecPlatform.Shared.Models;

namespace SpecPlatform.Api.Services;

public interface ICurrentUserService
{
    Guid? GetUserId();
    Task<User?> GetUserAsync(CancellationToken cancellationToken = default);
}
