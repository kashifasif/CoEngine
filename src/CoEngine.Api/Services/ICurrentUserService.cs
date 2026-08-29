using CoEngine.Shared.Models;

namespace CoEngine.Api.Services;

public interface ICurrentUserService
{
    Guid? GetUserId();
    Task<User?> GetUserAsync(CancellationToken cancellationToken = default);
}
