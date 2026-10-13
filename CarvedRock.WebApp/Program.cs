using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using CarvedRock.Core;
using CarvedRock.WebApp;
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IClaimsTransformation, AdminClaimsTransformation>();
builder.Services.AddHttpClient<IProductService, ProductService>(client => client.BaseAddress = new Uri("https://api"));
builder.Services.AddHttpClient("AI", client => client.BaseAddress = new Uri("https://api"));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<IEmailSender, EmailService>();


builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme =
        OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options => options.AccessDeniedPath = "/AccessDenied")
.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Auth:Authority"];
    options.ClientId = "interactive.confidential";
    options.ClientSecret = builder.Configuration["Auth:ClientSecret"];

    options.ResponseType = "code";
    options.UsePkce = true;
    options.SaveTokens = true;
    options.MapInboundClaims = false;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.TokenValidationParameters.NameClaimType = "email";

    options.Scope.Add("email");
    options.Scope.Add("api");
    options.Scope.Add("offline_access");
});


var app = builder.Build();
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserScopeMiddleware>();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (HttpRequestException exception) when (!context.Response.HasStarted && exception.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    { await context.ChallengeAsync(); }
    catch (HttpRequestException exception) when (!context.Response.HasStarted && exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
    { await context.ForbidAsync(); }
});

app.MapStaticAssets();
app.MapRazorPages()
    .RequireAuthorization()
    .WithStaticAssets();
app.Run();

