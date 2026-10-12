using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CarvedRock.WebApp.Pages;

public class CartModel(CartService cart) : PageModel
{
    public List<CartItem> Items { get; private set; } = [];
    public async Task OnGetAsync() => Items = await cart.GetItemsAsync();
    public async Task<IActionResult> OnPostAddAsync(int id)
    {
        try { await cart.AddAsync(id); }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToPage();
    }
    public IActionResult OnPostRemove(int id) { cart.Remove(id); return RedirectToPage(); }
    public IActionResult OnPostCancelOrder() { cart.Clear(); return RedirectToPage("/Index"); }
}
