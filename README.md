


## To Add Aspire 
curl -sSL https://aspire.dev/install.sh | bash

aspire --version


aspire init # to scaffold Aspire support 

dotnet add YourAppHost/YourAppHost.csproj package Aspire.Hosting.PostgreSQL

dotnet add CarvedRock.AppHost/CarvedRock.AppHost.csproj reference CarvedRock.Api/CarvedRock.Api.csproj


dotnet add CarvedRock.AppHost/CarvedRock.AppHost.csproj package CommunityToolkit.Aspire.Hosting.MailPit


## Identity server secrets 
how to set 
```bash
dotnet user-secrets set \
  "Parameters:webapp-client-secret" \
  "$(openssl rand -hex 32)" \
  --project CarvedRock.AppHost
```
how to check secret 
```bash 
dotnet user-secrets list --project CarvedRock.AppHost
```

```bash
dotnet user-secrets set \
  "Parameters:openai-api-key" \
  "YOUR_OPENAI_API_KEY" \
  --project CarvedRock.AppHost/CarvedRock.AppHost.csproj

```


aspire add communitytoolkit-mcpinspector \
  --apphost CarvedRock.AppHost/CarvedRock.AppHost.csproj \
  --version 13.6.0 \
  --non-interactive

## WebApp features

The WebApp now includes authenticated product/category listing, admin CRUD pages,
login/logout and access-denied pages, a session cart, and an email-confirmation
checkout. Bob (`bob` / `bob`) is the tutorial admin; Alice (`alice` / `alice`) is
an ordinary user. API product reads remain protected, unlike the original tutorial.

Open `/Listing?cat=all` to shop and `/Admin` to manage products. Adding to the cart
uses antiforgery-protected forms. Cart prices are fetched again from the API;
deleted products are omitted and quantities are capped at 99. Checkout sends a
confirmation to the user's email via Aspire's local Mailpit SMTP resource. Open
Mailpit's HTTP URL in the dashboard to read it. This is a tutorial checkout:
orders are not persisted and no payment is taken. Session carts are cleared when
the WebApp restarts or the user signs out.

Swagger's **Authorize** button uses the local IdentityServer with PKCE. Its
callback URL is registered from the Aspire API HTTPS endpoint. MCP tools attach
the caller's saved JWT only to the CarvedRock API HTTP client. API and WebApp
publish `/health` and `/alive`, and request logs include authenticated user context.

Restart the AppHost after changing its model or IdentityServer client registrations.
The `ApiUrl`, `WebAppUrl`, and client-secret settings are supplied by the AppHost.
Sign out and sign back in to update an existing WebApp session's claims.

## Authorization and feature checks

Product reads require authentication. Create, update, and delete require the
`admin` role. The local demo maps `BobSmith@email.com` (`bob` / `bob`) to admin;
`alice` / `alice` remains an ordinary user.

Run the repeatable authorization checks:

```bash
dotnet run --project tests/CarvedRock.AuthorizationChecks
```

These checks exercise the real product controller with locally signed JWTs and
an in-memory repository: anonymous requests return 401, ordinary users receive
403 for writes, and Bob can create (201), update (200), and delete (204).
They do not modify PostgreSQL or test the IdentityServer browser login flow.

The checks also cover MCP token forwarding and cart quantity/price handling.
For live verification after starting Aspire, use the current generated CA bundle
from the `mcp-inspector` resource's `NODE_EXTRA_CA_CERTS` environment variable:

```bash
NODE_EXTRA_CA_CERTS=/path/to/aspire/cert.pem node tests/webapp-smoke.mjs https://localhost:7013 --mcp
```

`--checkout` additionally sends one confirmation through the configured local
Mailpit resource. `IDENTITYSERVER_URL` and `MCP_URL` override the local smoke
check's defaults if the endpoint ports change. The live checks use the demo
Alice/Bob accounts and do not modify products.

For manual checks, send the access token as `Authorization: Bearer <token>` to
the API URL shown by Aspire. Use a disposable test product for PUT/DELETE.

## MCP Inspector login

Start the solution with `aspire run --apphost CarvedRock.AppHost/CarvedRock.AppHost.csproj`.
The AppHost runs a development-only identity server and registers the MCP HTTPS
endpoint as an OAuth resource. In MCP Inspector, connect to the MCP HTTPS URL
shown by Aspire and enter `interactive.public` in Authentication > OAuth 2.0
Flow > Client ID. Leave Client Secret empty. Use the scope
`api openid profile email offline_access`.

Sign in with `alice` / `alice` or `bob` / `bob`. The identity server accepts the
Inspector callback at `http://localhost:6274/oauth/callback` and its debug
variant. If Inspector asks for consent, choose **Allow** on the local consent
page. These sample accounts and the developer signing key are for local
development only.
