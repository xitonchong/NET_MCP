


## To Add Aspire 
curl -sSL https://aspire.dev/install.sh | bash

aspire --version


aspire init # to scaffold Aspire support 

dotnet add YourAppHost/YourAppHost.csproj package Aspire.Hosting.PostgreSQL

dotnet add CarvedRock.AppHost/CarvedRock.AppHost.csproj reference CarvedRock.Api/CarvedRock.Api.csproj


dotnet add CarvedRock.AppHost/CarvedRock.AppHost.csproj package CommunityToolkit.Aspire.Hosting.MailPit