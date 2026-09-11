using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CoEngine.Api.Infrastructure;

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
        var token = Request.Cookies["coengine_session"] 
            ?? Request.Cookies["spec_user_session"];

        if (string.IsNullOrWhiteSpace(token))
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }
            else if (Request.Headers.TryGetValue("X-Session-Token", out var headerVal))
            {
                token = headerVal.ToString().Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Guid userId;
        var parts = token.Split('_');
        if (parts.Length >= 3 && Guid.TryParse(parts[2], out userId))
        {
            // Valid gh_session_{userId}_{guid} format
        }
        else if (Guid.TryParse(token, out userId))
        {
            // Direct Guid userId
        }
        else
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid session token format."));
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.AuthenticationMethod, "SessionToken"),
            new Claim("Token", token),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
