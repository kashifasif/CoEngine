using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpecPlatform.Api.Data;
using SpecPlatform.Shared.Models;

namespace SpecPlatform.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public Guid? GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return null;
        
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    public async Task<User?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            return await _db.Users.FindAsync(new object[] { userId.Value }, cancellationToken);
        }

        return null;
    }
}
