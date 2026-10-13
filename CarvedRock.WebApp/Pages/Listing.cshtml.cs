using System.Net.Http.Headers;
using System.Text.Json;
using CarvedRock.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CarvedRock.WebApp.Pages;

public class ListingModel(
    IProductService products,
    IHttpClientFactory httpClientFactory) : PageModel
{
    public List<ProductModel> Products { get; private set; } = [];
    public string CategoryName { get; private set; } = "all";
    public async Task<IActionResult> OnGetAsync(string cat = "all")
    {
        if (cat != "all" && !Constants.Categories.Contains(cat)) return BadRequest();
        CategoryName = cat;
        Products = await products.GetProductsAsync(cat);
        return Page();
    }

    public async Task<IActionResult> OnGetChatAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest("A message is required.");
        }

        var accessToken = await HttpContext.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Challenge();
        }

        var client = httpClientFactory.CreateClient("AI");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"Agent?message={Uri.EscapeDataString(message)}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var apiResponse = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        apiResponse.EnsureSuccessStatusCode();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        await using var responseStream =
            await apiResponse.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var text in JsonSerializer.DeserializeAsyncEnumerable<string>(
            responseStream,
            cancellationToken: cancellationToken))
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var eventData = JsonSerializer.Serialize(text);

            await Response.WriteAsync(
                $"data: {eventData}\n\n",
                cancellationToken);

            await Response.Body.FlushAsync(cancellationToken);
        }

        await Response.WriteAsync(
            "event: end\ndata: done\n\n",
            cancellationToken);

        await Response.Body.FlushAsync(cancellationToken);

        return new EmptyResult();
    }
}
