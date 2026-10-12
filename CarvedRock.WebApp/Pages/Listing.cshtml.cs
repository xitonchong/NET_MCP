using CarvedRock.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CarvedRock.WebApp.Pages;

public class ListingModel(IProductService products) : PageModel
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
}
