using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SpecPlatform.Api.Infrastructure;

public class SessionAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public SessionAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        var xAuthToken = Request.Headers["X-Auth-Token"].FirstOrDefault();

        var token = !string.IsNullOrWhiteSpace(authHeader)
            ? authHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim()
            : xAuthToken?.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Validate token format (GitHub session or test tokens)
        if (!token.StartsWith("gh_session_", StringComparison.OrdinalIgnoreCase) &&
            !token.StartsWith("mock_dev_code_", StringComparison.OrdinalIgnoreCase) &&
            !token.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid session token format."));
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.AuthenticationMethod, "SessionToken"),
            new Claim("Token", token)
        };

        var parts = token.Split('_');
        if (parts.Length >= 3 && int.TryParse(parts[2], out var userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
