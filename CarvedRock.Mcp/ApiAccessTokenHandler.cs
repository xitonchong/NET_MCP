using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CarvedRock.Mcp;

public sealed class ApiAccessTokenHandler(IHttpContextAccessor contextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("No authenticated MCP request is available.");
        var token = await context.GetTokenAsync(JwtBearerDefaults.AuthenticationScheme, "access_token");
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("An authenticated MCP access token is required.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
