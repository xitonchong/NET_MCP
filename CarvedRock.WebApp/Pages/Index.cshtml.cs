using System.Net;
using CarvedRock.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CarvedRock.WebApp.Pages;

public class IndexModel(IProductService products, ILogger<IndexModel> logger) : PageModel
{
    public List<ProductModel> Products { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public async Task<IActionResult> OnGetAsync()
    {
        try { Products = await products.GetProductsAsync(); }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        { return Challenge(); }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Unable to load products.");
            ErrorMessage = "Products could not be loaded. Please try again later.";
        }
        return Page();
    }
}
