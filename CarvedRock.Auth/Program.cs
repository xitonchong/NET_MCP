using System.Net;
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("The sample identity server is for local development only.");
}

var mcpResource = builder.Configuration["McpServerUrl"]?.TrimEnd('/') + "/";
if (!Uri.TryCreate(mcpResource, UriKind.Absolute, out _))
{
    throw new InvalidOperationException("McpServerUrl must be an absolute URL.");
}

builder.Services.AddIdentityServer(options =>
    {
        options.UserInteraction.LoginUrl = "/login";
        options.UserInteraction.ConsentUrl = "/consent";
    })
    .AddInMemoryIdentityResources([
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ])
    .AddInMemoryApiScopes([new ApiScope("api")])
    .AddInMemoryApiResources([
        new ApiResource(mcpResource, "CarvedRock MCP", ["email"])
        {
            Scopes = { "api" }
        }
    ])
    .AddInMemoryClients([
        new Client
        {
            ClientId = "interactive.public",
            ClientName = "MCP Inspector",
            RequireClientSecret = false,
            RequirePkce = true,
            RequireConsent = false,
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris =
            {
                "http://localhost:6274/oauth/callback",
                "http://localhost:6274/oauth/callback/debug"
            },
            AllowedCorsOrigins = { "http://localhost:6274" },
            AllowedScopes = { "openid", "profile", "email", "api" },
            AllowOfflineAccess = true
        }
    ])
    .AddTestUsers([
        new TestUser
        {
            SubjectId = "1",
            Username = "alice",
            Password = "alice",
            Claims = { new Claim("email", "AliceSmith@email.com") }
        },
        new TestUser
        {
            SubjectId = "2",
            Username = "bob",
            Password = "bob",
            Claims = { new Claim("email", "BobSmith@email.com") }
        }
    ])
    .AddDeveloperSigningCredential();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseIdentityServer();

app.MapGet("/login", (string? returnUrl) =>
{
    var safeReturnUrl = WebUtility.HtmlEncode(returnUrl ?? "");
    return Results.Content($$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>CarvedRock development login</title></head>
        <body>
          <h1>CarvedRock development login</h1>
          <p>Use alice/alice or bob/bob.</p>
          <form method="post" action="/login">
            <input type="hidden" name="returnUrl" value="{{safeReturnUrl}}">
            <label>Username <input name="username" required></label>
            <label>Password <input name="password" type="password" required></label>
            <button type="submit">Sign in</button>
          </form>
        </body>
        </html>
        """, "text/html");
});

app.MapPost("/login", async (HttpContext context, TestUserStore users,
    IIdentityServerInteractionService interaction) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (!interaction.IsValidReturnUrl(returnUrl))
    {
        return Results.BadRequest("Invalid authorization return URL.");
    }

    if (!users.ValidateCredentials(username, password))
    {
        return Results.BadRequest("Invalid username or password.");
    }

    var user = users.FindByUsername(username);
    await context.SignInAsync(new IdentityServerUser(user.SubjectId)
    {
        DisplayName = user.Username
    });
    return Results.Redirect(returnUrl);
});

app.MapGet("/consent", async (string? returnUrl,
    IIdentityServerInteractionService interaction) =>
{
    var request = await interaction.GetAuthorizationContextAsync(returnUrl);
    if (request is null)
    {
        return Results.BadRequest("Invalid authorization request.");
    }

    var clientName = WebUtility.HtmlEncode(request.Client.ClientName ?? request.Client.ClientId);
    var scopes = WebUtility.HtmlEncode(string.Join(", ", request.ValidatedResources.RawScopeValues));
    var safeReturnUrl = WebUtility.HtmlEncode(returnUrl ?? "");
    return Results.Content($$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>CarvedRock development consent</title></head>
        <body>
          <h1>Allow {{clientName}}?</h1>
          <p>Requested access: {{scopes}}</p>
          <form method="post" action="/consent">
            <input type="hidden" name="returnUrl" value="{{safeReturnUrl}}">
            <button name="decision" value="approve" type="submit">Allow</button>
            <button name="decision" value="deny" type="submit">Deny</button>
          </form>
        </body>
        </html>
        """, "text/html");
});

app.MapPost("/consent", async (HttpContext context,
    IIdentityServerInteractionService interaction) =>
{
    var form = await context.Request.ReadFormAsync();
    var returnUrl = form["returnUrl"].ToString();
    var request = await interaction.GetAuthorizationContextAsync(returnUrl);
    if (request is null)
    {
        return Results.BadRequest("Invalid authorization request.");
    }

    var response = form["decision"] == "approve"
        ? new ConsentResponse
        {
            RememberConsent = true,
            ScopesValuesConsented = request.ValidatedResources.RawScopeValues.ToArray()
        }
        : new ConsentResponse { Error = AuthorizationError.AccessDenied };

    await interaction.GrantConsentAsync(request, response);
    return Results.Redirect(returnUrl);
});

app.Run();
