var builder = DistributedApplication.CreateBuilder(args);



var webAppClientSecret = builder.AddParameter(
    "webapp-client-secret",
    secret: true);

var carvedrockdb = builder.AddPostgres("postgres")
                          .WithImageTag("18.3-alpine")
                          .AddDatabase("CarvedRock"); 



var api = builder.AddProject<Projects.CarvedRock_Api>("api") 
    .WithReference(carvedrockdb) 
    .WaitFor(carvedrockdb)
    .WithHttpHealthCheck("/health");


var mailpit = builder.AddMailPit("smtp");



var webapp = builder.AddProject<Projects.CarvedRock_WebApp>("webapp")
    .WithReference(api) 
    .WithReference(mailpit) 
    .WaitFor(mailpit) 
    .WaitFor(api)
    .WithHttpHealthCheck("/health");

var mcp = builder.AddProject<Projects.CarvedRock_Mcp>("mcp");



var idsrv = builder.AddProject<Projects.Duende_IdentityServer_Demo>("idsrv")
    .WithEnvironment("McpServerUrl", mcp.GetEndpoint("https"))
    .WithEnvironment("WebAppUrl", webapp.GetEndpoint("https"))
    .WithEnvironment("ApiUrl", api.GetEndpoint("https"))
    .WithEnvironment("WebAppClientSecret", webAppClientSecret)
    .WithHttpHealthCheck("/health");




api.WithEnvironment("Auth__Authority", idsrv.GetEndpoint("https"))
    .WaitFor(idsrv);

webapp
    .WithEnvironment("Auth__Authority", idsrv.GetEndpoint("https"))
    .WithEnvironment("Auth__ClientSecret", webAppClientSecret)
    .WaitFor(idsrv);

mcp
    .WithReference(api)
    .WithEnvironment(
        "Auth__Authority",
        idsrv.GetEndpoint("https"))
    .WithEnvironment(
        "McpServerUrl",
        mcp.GetEndpoint("https"))
    .WaitFor(api)
    .WaitFor(idsrv)
    .WithHttpHealthCheck("/health");


builder.AddMcpInspector("mcp-inspector", options =>
{
    options.InspectorVersion = "1.0.2";
})
    .WithMcpServer(mcp, path: "");


builder.Build().Run();
