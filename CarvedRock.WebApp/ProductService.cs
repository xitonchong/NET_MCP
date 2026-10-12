using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CarvedRock.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace CarvedRock.WebApp;

public interface IProductService
{
    Task<List<ProductModel>> GetProductsAsync(string category = "all");
    Task<ProductModel?> GetProductByIdAsync(int id);
    Task<IDictionary<string, string>> AddProductAsync(NewProductModel product);
    Task<IDictionary<string, string>> UpdateProductAsync(int id, NewProductModel product);
    Task<bool> DeleteProductAsync(int id);
}

public sealed class ProductService(HttpClient client, IHttpContextAccessor contextAccessor) : IProductService
{
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("An HTTP request is required.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new HttpRequestException("Please sign in again.", null, HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request, context.RequestAborted);
    }

    public async Task<List<ProductModel>> GetProductsAsync(string category = "all")
    {
        using var response = await SendAsync(HttpMethod.Get, $"/product?category={Uri.EscapeDataString(category)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProductModel>>() ?? [];
    }

    public async Task<ProductModel?> GetProductByIdAsync(int id)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/product/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductModel>();
    }

    public Task<IDictionary<string, string>> AddProductAsync(NewProductModel product) => WriteAsync(HttpMethod.Post, "/product", product);
    public Task<IDictionary<string, string>> UpdateProductAsync(int id, NewProductModel product) => WriteAsync(HttpMethod.Put, $"/product/{id}", product);

    private async Task<IDictionary<string, string>> WriteAsync(HttpMethod method, string path, NewProductModel product)
    {
        using var response = await SendAsync(method, path, product);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new KeyNotFoundException("Product no longer exists.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var details = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            var errors = new Dictionary<string, string>();
            if (details is not null)
            {
                foreach (var (key, value) in details.Extensions)
                {
                    if (key == "traceId") continue;
                    if (key == "errors" && value is JsonElement { ValueKind: JsonValueKind.Object } element)
                        foreach (var property in element.EnumerateObject())
                            errors[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                                ? string.Join("|", property.Value.EnumerateArray().Select(v => v.GetString()))
                                : property.Value.ToString();
                    else errors[key] = value?.ToString() ?? "Invalid value.";
                }
                if (errors.Count == 0) errors[""] = details.Detail ?? details.Title ?? "Invalid product.";
            }
            else errors[""] = "Invalid product.";
            return errors;
        }
        response.EnsureSuccessStatusCode();
        return new Dictionary<string, string>();
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"/product/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
}
