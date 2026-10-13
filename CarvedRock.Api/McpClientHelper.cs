using System.Net.Http.Headers; 
using ModelContextProtocol.Client; 


namespace CarvedRock.Api; 


public static class McpClientHelper
{
    public static Task<McpClient> CreateAsync(
        IConfiguration configuration, 
        HttpContext httpContext, 
        CancellationToken cancellationToken
    )
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("An authenticated API request is required."); 
        }
        var rawHeader = httpContext.Request.Headers.Authorization.ToString(); 

        if (!AuthenticationHeaderValue.TryParse(rawHeader, out var authorization)
            || !authorization.Scheme.Equals(
                "Bearer", StringComparison.OrdinalIgnoreCase) 
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new UnauthorizedAccessException("A bearer access token is required."); 
        }

        var configuredUrl = configuration["McpServerUrl"]; 

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var mcpUrl))
        {
            throw new InvalidOperationException("McpServerUrl must be an absolute URL"); 
        }

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = mcpUrl, 
            TransportMode = HttpTransportMode.StreamableHttp, 
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {authorization.Parameter}"
            }
        }; 
        return McpClient.CreateAsync(
            new HttpClientTransport(transportOptions), 
            cancellationToken: cancellationToken
        ); 
    }
}