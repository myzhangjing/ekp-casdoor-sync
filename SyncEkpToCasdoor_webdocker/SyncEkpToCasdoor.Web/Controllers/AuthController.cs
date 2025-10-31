using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Casdoor.AspNetCore;

namespace SyncEkpToCasdoor.Web.Controllers;

public class AuthController : Controller
{
    [HttpGet("/challenge")]
    public new IActionResult Challenge()
    {
        // 使用 Casdoor SDK 的认证方案
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/"
        };
        return base.Challenge(properties, "Casdoor");
    }

    [HttpGet("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}
