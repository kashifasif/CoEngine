using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CoEngine.Api.Data;
using CoEngine.Shared.Models;

namespace CoEngine.Api.Services;

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
            var user = await _db.Users.FindAsync(new object[] { userId.Value }, cancellationToken);
            if (user == null)
            {
                user = new User
                {
                    Id = userId.Value,
                    Username = "testuser",
                    DisplayName = "Test User",
                    Email = "test@coengine.dev",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return user;
        }

        return null;
    }
}
