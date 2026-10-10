# MCP Inspector OAuth login

This project uses a local, development-only Duende IdentityServer so Inspector
can obtain an access token for the MCP server. The public
`demo.duendesoftware.com` server could not issue a token for this project's
`https://localhost:7275/` resource and returned `invalid_target`.

## What made the flow work

1. **Protect the MCP endpoint.** `CarvedRock.Mcp/Program.cs` validates JWT bearer
   tokens, publishes OAuth protected-resource metadata, and requires
   authorization on `MapMcp()`. An unauthenticated request receives `401` with
   a `resource_metadata` URL; that is how Inspector discovers the issuer.
2. **Run a local issuer in Aspire.** `CarvedRock.Auth` defines the `api` scope,
   test users, login page, and Inspector OAuth client. `AppHost.cs` passes the
   auth HTTPS endpoint to MCP and API as `Auth__Authority`.
3. **Register the same resource URL at both ends.** Aspire passes the MCP HTTPS
   endpoint to the issuer and MCP server. Both normalize it to include a
   trailing slash, such as `https://localhost:7275/`. The issuer registers
   that URL as an API resource, and MCP validates it as the token audience.
   The scheme, port, and trailing slash must match Inspector's `resource`
   parameter.
4. **Use a pre-registered public client.** The local issuer registers
   `interactive.public` for Authorization Code with PKCE, no client secret,
   and Inspector's normal and debug callback URLs. Enter this client ID in
   Inspector's **Authentication > OAuth 2.0 Flow** panel. Inspector 1.0.2
   stores the field in the browser; setting `OAUTH_CLIENT_ID` on the Aspire
   Inspector process does not populate it. Without a client ID, Inspector
   attempts dynamic client registration and fails because the issuer does not
   offer a registration endpoint.
5. **Handle login and consent.** Sign in to the local issuer with `alice` /
   `alice` or `bob` / `bob`. Inspector may send `prompt=consent` even though
   the client normally skips consent. The local `/consent` page handles that
   request; choose **Allow** to continue to Inspector's callback.

## Key code

### Aspire wiring

In `CarvedRock.AppHost/AppHost.cs`, the issuer and MCP server receive the
same MCP endpoint, while MCP and API receive the local issuer endpoint:

```csharp
var mcp = builder.AddProject<Projects.CarvedRock_Mcp>("mcp");
var auth = builder.AddProject<Projects.CarvedRock_Auth>("auth")
    .WithEnvironment("McpServerUrl", mcp.GetEndpoint("https"))
    .WithHttpHealthCheck("/health");

api.WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
    .WaitFor(auth);

mcp.WithReference(api)
    .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
    .WithEnvironment("McpServerUrl", mcp.GetEndpoint("https"))
    .WaitFor(api)
    .WaitFor(auth)
    .WithHttpHealthCheck("/health");
```

### Issuer resource and client

`CarvedRock.Auth/Program.cs` registers the MCP URL, `api` scope, and the
preconfigured public client. The URL is normalized to include the slash that
Inspector sends in its `resource` parameter:

```csharp
var mcpResource = builder.Configuration["McpServerUrl"]?.TrimEnd('/') + "/";

builder.Services.AddIdentityServer(options =>
    {
        options.UserInteraction.LoginUrl = "/login";
        options.UserInteraction.ConsentUrl = "/consent";
    })
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
    ]);
```

The complete registration in that file also adds the `openid`, `profile`, and
`email` identity resources, the `alice` and `bob` test users, and a developer
signing key.

### MCP authentication

`CarvedRock.Mcp/Program.cs` publishes resource metadata and checks the
token's issuer and audience:

```csharp
var authority = builder.Configuration["Auth:Authority"]
    ?? throw new InvalidOperationException("Auth:Authority is not configured.");
var mcpServerUrl = (builder.Configuration["McpServerUrl"]
    ?? throw new InvalidOperationException("McpServerUrl is not configured."))
    .TrimEnd('/') + "/";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidIssuer = authority,
            ValidAudience = mcpServerUrl,
            NameClaimType = ClaimTypes.Email
        };
    })
    .AddMcp(options =>
    {
        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = new Uri(mcpServerUrl),
            AuthorizationServers = { new Uri(authority) },
            ScopesSupported = ["api", "openid", "profile", "email", "offline_access"]
        };
    });

builder.Services.AddAuthorization();

// After builder.Build():
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp().RequireAuthorization();
```

### Login and consent

The local login handler validates the return URL and test-user credentials,
then issues the IdentityServer cookie:

```csharp
if (!interaction.IsValidReturnUrl(returnUrl))
    return Results.BadRequest("Invalid authorization return URL.");
if (!users.ValidateCredentials(username, password))
    return Results.BadRequest("Invalid username or password.");

var user = users.FindByUsername(username);
await context.SignInAsync(new IdentityServerUser(user.SubjectId)
{
    DisplayName = user.Username
});
return Results.Redirect(returnUrl);
```

Inspector can force consent with `prompt=consent`. The `/consent` POST handler
uses IdentityServer's authorization context to approve or deny the request:

```csharp
var request = await interaction.GetAuthorizationContextAsync(returnUrl);
if (request is null)
    return Results.BadRequest("Invalid authorization request.");

var response = form["decision"] == "approve"
    ? new ConsentResponse
    {
        RememberConsent = true,
        ScopesValuesConsented = request.ValidatedResources.RawScopeValues.ToArray()
    }
    : new ConsentResponse { Error = AuthorizationError.AccessDenied };

await interaction.GrantConsentAsync(request, response);
return Results.Redirect(returnUrl);
```

The full HTML forms and handlers are in `CarvedRock.Auth/Program.cs`.

## Repeat the login

1. Run `aspire run --apphost CarvedRock.AppHost/CarvedRock.AppHost.csproj`.
2. Open the MCP Inspector URL shown in the Aspire dashboard. Connect with
   Streamable HTTP to the MCP HTTPS URL shown there (currently
   `https://localhost:7275/`).
3. Under **Authentication > OAuth 2.0 Flow**, set Client ID to
   `interactive.public`, leave Client Secret empty, and use scopes
   `api openid profile email offline_access`.
4. Start OAuth, sign in at the local issuer (currently
   `https://localhost:7385`), and choose **Allow** if prompted.
5. In Inspector's debug flow, copy the authorization code from the callback
   into its Authorization Code field, then continue the token exchange.
   The normal flow handles the callback automatically.

If an older authorization tab has expired after restarting Aspire, start OAuth
again from Inspector. These users and the developer signing key are for local
development only.
