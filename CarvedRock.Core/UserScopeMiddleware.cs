using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CarvedRock.Core;

public sealed class UserScopeMiddleware(RequestDelegate next, ILogger<UserScopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["User"] = context.User.Identity.Name,
                ["SubjectId"] = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
            await next(context);
        }
        else await next(context);
    }
}
