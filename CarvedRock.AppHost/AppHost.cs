var builder = DistributedApplication.CreateBuilder(args);



var carvedrockdb = builder.AddPostgres("postgres")
                          .WithImageTag("18.3-alpine")
                          .AddDatabase("CarvedRock"); 



var api = builder.AddProject<Projects.CarvedRock_Api>("api") 
    .WithReference(carvedrockdb) 
    .WaitFor(carvedrockdb); 
    // .WithHttpHealthCheck("/health"); 


var mailpit = builder.AddMailPit("smtp");


builder.AddProject<Projects.CarvedRock_WebApp>("webapp")
    .WithReference(api) 
    .WithReference(mailpit) 
    .WaitFor(mailpit) 
    .WaitFor(api);
    // .WithHttpHealthCheck("/health"); 



builder.Build().Run();


