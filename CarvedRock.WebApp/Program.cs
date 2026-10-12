using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect; 

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); 

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddHttpClient("CarvedRockApi", client =>
{
    client.BaseAddress = new Uri("https://api");
}).AddServiceDiscovery();


builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = 
        CookieAuthenticationDefaults.AuthenticationScheme; 
    options.DefaultChallengeScheme = 
        OpenIdConnectDefaults.AuthenticationScheme; 
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Auth:Authority"]; 
    options.ClientId = "interactive.confidential";
    options.ClientSecret = builder.Configuration["Auth:ClientSecret"];

    options.ResponseType = "code";
    options.UsePkce = true;
    options.SaveTokens = true;

    options.Scope.Add("email");
    options.Scope.Add("api");
}); 


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication(); 
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .RequireAuthorization()
    .WithStaticAssets();
app.Run();

