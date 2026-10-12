using CarvedRock.Mcp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens; 
using System.Security.Claims;
using ModelContextProtocol.Authentication; 

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var authority = builder.Configuration["Auth:Authority"]
    ?? throw new InvalidOperationException("Auth:Authority is not configured."); 

var mcpServerUrl = (builder.Configuration["McpServerUrl"]
    ?? throw new InvalidOperationException("McpServerUrl is not configured.")).TrimEnd('/') + "/";


builder.Services.AddCors(options =>
{
    options.AddPolicy("McpOAuth", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    }); 
}); 

builder.Services    
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; 
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
      options.SaveToken = true;
      options.Authority = authority;
      options.TokenValidationParameters = 
        new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, 
            ValidIssuer = authority, 
            ValidAudience = mcpServerUrl, 
            NameClaimType = ClaimTypes.Email
        };
    })
    .AddMcp(options =>
    {
        options.ResourceMetadata = 
            new ProtectedResourceMetadata
            {
                Resource = new Uri(mcpServerUrl), 
                AuthorizationServers =
                {
                    new Uri(authority)
                },
                ScopesSupported =
                [
                    "api", 
                    "openid", 
                    "profile", 
                    "email", 
                    "offline_access"
                ]
            }; 
    }); 

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ApiAccessTokenHandler>();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<CarvedRockTools>()
    .WithTools<AdminTools>();

builder.Services.AddHttpClient("CarvedRockApi", client =>
{
    client.BaseAddress = new Uri("https://api");
}).AddHttpMessageHandler<ApiAccessTokenHandler>();

var app = builder.Build();

app.MapDefaultEndpoints();



if (app.Environment.IsDevelopment())
{
    app.UseCors("McpOAuth");
}
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp()
    .RequireAuthorization();

app.Run();
