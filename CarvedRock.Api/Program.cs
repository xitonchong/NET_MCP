
using CarvedRock.Api; 

using CarvedRock.Data; 
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics; 
using System.IdentityModel.Tokens.Jwt; 


var builder = WebApplication.CreateBuilder(args); 


var authority = builder.Configuration.GetValue<string>("Auth:Authority"); 
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;  

builder.Services.AddAuthentication("Bearer") 
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = authority; 
        options.TokenValidationParaeters = new TokenValidationParameters
        {
            NameClaimType = "email",
            //RoleClaimType = "role",
            ValidateAudience = false
        };
    }); 

builder.Services.AddDbContext<LocalContext>(options => 
    options.UseNpgsql(builder.Configuration.GetConnectionString("CarvedRock"))); 

builder.Services.AddScoped<ICarvedRockRepository, CarvedRockRepository>(); 

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LocalContext>();
    context.MigrateAndCreateData();
}

app.MapGet("/products", async (ICarvedRockRepository repository) =>
    await repository.GetProductsAsync("all"));

app.Run();
