using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SpecPlatform.Api.Infrastructure;

public class RequireAuthorizationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Allow public access to action/controller decorated with [AllowAnonymous]
        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
        {
            await next();
            return;
        }

        // Check for authorization token in headers (Authorization: Bearer <token> or X-Auth-Token)
        var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        var xAuthToken = context.HttpContext.Request.Headers["X-Auth-Token"].FirstOrDefault();

        var token = !string.IsNullOrWhiteSpace(authHeader)
            ? authHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim()
            : xAuthToken?.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "Unauthorized access. Valid authentication token required via X-Auth-Token or Authorization header."
            });
            return;
        }

        await next();
    }
}
