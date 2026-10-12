using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CarvedRock.WebApp.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null) => Challenge(new AuthenticationProperties
    {
        RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/"
    });
}
