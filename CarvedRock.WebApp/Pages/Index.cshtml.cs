using System.Net;
using System.Net.Http.Headers;
using CarvedRock.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarvedRock.WebApp.Pages;

public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public List<ProductModel> Products { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;
        var accessToken = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Challenge();
        }

        var client = httpClientFactory.CreateClient("CarvedRockApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/product");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Challenge();
            }

            response.EnsureSuccessStatusCode();
            Products = await response.Content.ReadFromJsonAsync<List<ProductModel>>(cancellationToken)
                ?? [];
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Unable to load products from CarvedRock API.");
            ErrorMessage = "Products could not be loaded. Please try again later.";
        }

        return Page();
    }
}
