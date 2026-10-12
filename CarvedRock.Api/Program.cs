
using CarvedRock.Api; 
using CarvedRock.Core; 

using CarvedRock.Data; 
using CarvedRock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics; 
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;


using Microsoft.Extensions.Hosting; 
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args); 
builder.AddServiceDefaults(); 

var authority = builder.Configuration.GetValue<string>("Auth:Authority"); 
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;  

builder.Services.AddAuthentication("Bearer") 
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = authority; 
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "email",
            //RoleClaimType = "role",
            ValidateAudience = false
        };
    }); 

builder.Services.AddTransient<IClaimsTransformation, AdminClaimsTransformation>(); 
builder.Services.AddScoped<IProductLogic, ProductLogic>();
builder.Services.AddScoped<NewProductValidator>(); 


builder.Services.AddDbContext<LocalContext>(options => 
    options.UseNpgsql(builder.Configuration.GetConnectionString("CarvedRock"))); 

builder.Services.AddScoped<ICarvedRockRepository, CarvedRockRepository>(); 

builder.Services.AddControllers(); 
builder.Services.AddEndpointsApiExplorer(); 
builder.Services.AddSwaggerGen(); 
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerOptions>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    if (context.ProblemDetails.Status == 500)
        context.ProblemDetails.Detail = "An error occurred in our API. Use the trace ID when contacting us.";
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserScopeMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LocalContext>();
    context.MigrateAndCreateData();
}

app.MapControllers(); 


app.UseSwagger(); 
app.UseSwaggerUI(options =>
{
    options.OAuthClientId("interactive.public");
    options.OAuthAppName("CarvedRock API");
    options.OAuthUsePkce();
});



// app.MapGet("/products", async (ICarvedRockRepository repository) =>
//     await repository.GetProductsAsync("all"));

app.Run();
