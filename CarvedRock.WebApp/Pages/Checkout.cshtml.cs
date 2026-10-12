using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CarvedRock.WebApp.Pages;

public class CheckoutModel(CartService cart, IEmailSender emailSender, ILogger<CheckoutModel> logger) : PageModel
{
    public List<CartItem> Items { get; private set; } = [];
    public string? EmailAddress => User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
    public async Task<IActionResult> OnGetAsync()
    {
        Items = await cart.GetItemsAsync();
        return Items.Count == 0 ? RedirectToPage("/Cart") : Page();
    }
    public async Task<IActionResult> OnPostSubmitOrderAsync()
    {
        Items = await cart.GetItemsAsync();
        if (Items.Count == 0) return RedirectToPage("/Cart");
        if (string.IsNullOrWhiteSpace(EmailAddress))
        {
            ModelState.AddModelError("", "Your account needs an email address to place an order.");
            return Page();
        }
        var html = new StringBuilder("<h1>Thank you for your order!</h1><table><tr><th>Product</th><th>Quantity</th><th>Total</th></tr>");
        foreach (var item in Items)
            html.Append($"<tr><td>{WebUtility.HtmlEncode(item.Name)}</td><td>{item.Quantity}</td><td>{item.Total:N2}</td></tr>");
        html.Append($"</table><p>Total: {Items.Sum(i => i.Total):N2}</p>");
        try { await emailSender.SendEmailAsync(EmailAddress, "Your CarvedRock Order", html.ToString()); }
        catch (Exception exception) when (exception is System.Net.Mail.SmtpException or FormatException)
        {
            logger.LogError(exception, "Order confirmation email could not be sent.");
            ModelState.AddModelError("", "Unable to send the confirmation. Your cart has been retained; please try again.");
            return Page();
        }
        cart.Clear();
        return RedirectToPage("/ThankYou");
    }
}
