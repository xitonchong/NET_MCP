using System.Net;
using System.Diagnostics.CodeAnalysis;
using CarvedRock.Core;
using CarvedRock.Mcp;
using CarvedRock.WebApp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ProductModel = CarvedRock.Core.ProductModel;

internal static class FeatureChecks
{
    public static async Task RunAsync(IServiceProvider services, string token)
    {
        using var scope = services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Authorization = "Bearer " + token;
        var result = await context.AuthenticateAsync("Bearer");
        context.User = result.Principal!;
        var accessor = new HttpContextAccessor { HttpContext = context };
        var recorder = new RecordingHandler();
        using var forwarding = new ApiAccessTokenHandler(accessor) { InnerHandler = recorder };
        using var client = new HttpClient(forwarding);
        await client.GetAsync("https://api/product");
        Require(recorder.Token == token, "MCP forwards the current authenticated access token");
        context.User = new System.Security.Claims.ClaimsPrincipal();
        try
        {
            await client.GetAsync("https://api/product");
            throw new InvalidOperationException("Anonymous MCP request was forwarded.");
        }
        catch (UnauthorizedAccessException) { Console.WriteLine("PASS anonymous MCP requests are not forwarded"); }

        context.Session = new MemorySession();
        var products = new MemoryProductService();
        var cart = new CartService(accessor, products);
        for (var i = 0; i < 101; i++) await cart.AddAsync(1);
        var items = await cart.GetItemsAsync();
        Require(items.Single().Quantity == 99, "cart caps product quantities at 99");
        products.Price = 125;
        Require((await cart.GetItemsAsync()).Single().Total == 99 * 125, "cart calculates totals using current API prices");
        products.Deleted = true;
        Require((await cart.GetItemsAsync()).Count == 0, "cart ignores deleted products");
        cart.Clear();
        Require(context.Session.GetString("cart") is null, "cart clears after checkout/cancellation");
        accessor.HttpContext = null;
    }
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Console.WriteLine("PASS " + label);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Token { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Token = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> data = [];
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => data.Keys;
        public void Clear() => data.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => data.Remove(key);
        public void Set(string key, byte[] value) => data[key] = value;
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => data.TryGetValue(key, out value);
    }
    private sealed class MemoryProductService : IProductService
    {
        public double Price { get; set; } = 100;
        public bool Deleted { get; set; }
        private ProductModel Product => new() { Id = 1, Name = "Boot", Category = "boots", Description = "Test", ImgUrl = "https://example.com/boot.png", Price = Price };
        public Task<List<ProductModel>> GetProductsAsync(string category = "all") => Task.FromResult<List<ProductModel>>(Deleted ? [] : [Product]);
        public Task<ProductModel?> GetProductByIdAsync(int id) => Task.FromResult<ProductModel?>(id == 1 && !Deleted ? Product : null);
        public Task<IDictionary<string, string>> AddProductAsync(NewProductModel product) => throw new NotSupportedException();
        public Task<IDictionary<string, string>> UpdateProductAsync(int id, NewProductModel product) => throw new NotSupportedException();
        public Task<bool> DeleteProductAsync(int id) => throw new NotSupportedException();
    }
}
