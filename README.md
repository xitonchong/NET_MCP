


## To Add Aspire 
curl -sSL https://aspire.dev/install.sh | bash

aspire --version


aspire init # to scaffold Aspire support 

dotnet add YourAppHost/YourAppHost.csproj package Aspire.Hosting.PostgreSQL

dotnet add CarvedRock.AppHost/CarvedRock.AppHost.csproj reference CarvedRock.Api/CarvedRock.Api.csproj


dotnet add CarvedRock.AppHost/CarvedRock.AppHost.csproj package CommunityToolkit.Aspire.Hosting.MailPit





aspire add communitytoolkit-mcpinspector \
  --apphost CarvedRock.AppHost/CarvedRock.AppHost.csproj \
  --version 13.6.0 \
  --non-interactive

## Local MCP OAuth

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
