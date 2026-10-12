using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using CarvedRock.Api.Controllers;
using CarvedRock.Core;
using CarvedRock.Data;
using CarvedRock.Data.Entities;
using CarvedRock.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.IdentityModel.Tokens;

// This host uses the real controller and claims transformation, but never opens PostgreSQL.
var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
const string issuer = "carvedrock-authorization-checks";
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddAuthentication("Bearer").AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = issuer,
        ValidAudience = "api",
        IssuerSigningKey = key,
        NameClaimType = "email"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddTransient<IClaimsTransformation, AdminClaimsTransformation>();
var repository = new MemoryRepository();
builder.Services.AddSingleton<ICarvedRockRepository>(repository);
builder.Services.AddScoped<IProductLogic, ProductLogic>();
builder.Services.AddScoped<NewProductValidator>();
builder.Services.AddControllers().AddApplicationPart(typeof(ProductController).Assembly);
await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.StartAsync();
var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
using var client = new HttpClient { BaseAddress = new Uri(addresses.Addresses.Single()) };
var product = new NewProductModel
{
    Name = "Authorization check", Description = "In-memory test product",
    Category = "boots", Price = 100, ImgUrl = "https://example.com/boots.png"
};

string Token(string email) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
    issuer, "api", [new Claim("email", email)],
    expires: DateTime.UtcNow.AddMinutes(5),
    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));

async Task Check(HttpMethod method, string path, string? token, HttpStatusCode expected)
{
    using var request = new HttpRequestMessage(method, path);
    if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (method == HttpMethod.Put || method == HttpMethod.Post) request.Content = JsonContent.Create(product);
    using var response = await client.SendAsync(request);
    if (response.StatusCode != expected)
        throw new InvalidOperationException($"{method} {path}: expected {(int)expected}, got {(int)response.StatusCode}.");
    Console.WriteLine($"PASS {method} {path}: {(int)response.StatusCode}");
}

try
{
    var alice = Token("AliceSmith@email.com");
    var bob = Token("BobSmith@email.com");
    await Check(HttpMethod.Get, "/product", null, HttpStatusCode.Unauthorized);
    await Check(HttpMethod.Get, "/product", alice, HttpStatusCode.OK);
    foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
    {
        var path = method == HttpMethod.Post ? "/product" : "/product/1";
        await Check(method, path, null, HttpStatusCode.Unauthorized);
        await Check(method, path, alice, HttpStatusCode.Forbidden);
    }
    if (repository.Writes != 0) throw new InvalidOperationException("An unauthorized request changed products.");
    await Check(HttpMethod.Post, "/product", bob, HttpStatusCode.Created);
    await Check(HttpMethod.Put, "/product/1", bob, HttpStatusCode.OK);
    await Check(HttpMethod.Delete, "/product/1", bob, HttpStatusCode.NoContent);
    var transform = new AdminClaimsTransformation();
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "BobSmith@email.com")], "test"));
    var transformed = await transform.TransformAsync(await transform.TransformAsync(principal));
    if (transformed.Claims.Count(c => c.Type == ClaimTypes.Role && c.Value == "admin") != 1)
        throw new InvalidOperationException("Repeated transformation must add exactly one admin role.");
    if (principal.IsInRole("admin")) throw new InvalidOperationException("Transformation changed the original principal.");
    await FeatureChecks.RunAsync(app.Services, bob);
    Console.WriteLine("PASS repeated claims transformation; all authorization checks passed.");
}
finally
{
    await app.StopAsync();
}

sealed class MemoryRepository : ICarvedRockRepository
{
    private readonly Dictionary<int, Product> products = [];
    public int Writes { get; private set; }
    public Task<List<Product>> GetProductsAsync(string category) => Task.FromResult(products.Values.ToList());
    public Task<Product?> GetProductByIdAsync(int id) => Task.FromResult(products.GetValueOrDefault(id));
    public Task<bool> IsProductNameUniqueAsync(string name) => Task.FromResult(true);
    public Task<Product> CreateProductAsync(Product product)
    {
        product.Id = products.Count + 1;
        products[product.Id] = product;
        Writes++;
        return Task.FromResult(product);
    }
    public Task<Product> UpdateProductAsync(Product product)
    {
        if (!products.ContainsKey(product.Id)) throw new KeyNotFoundException();
        products[product.Id] = product;
        Writes++;
        return Task.FromResult(product);
    }
    public Task DeleteProductAsync(int id)
    {
        if (!products.Remove(id)) throw new KeyNotFoundException();
        Writes++;
        return Task.CompletedTask;
    }
}
