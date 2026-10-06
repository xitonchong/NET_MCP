
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LocalContext>();
    context.MigrateAndCreateData();
}

app.MapControllers(); 


app.UseSwagger(); 
app.UseSwaggerUI(); 



// app.MapGet("/products", async (ICarvedRockRepository repository) =>
//     await repository.GetProductsAsync("all"));

app.Run();
