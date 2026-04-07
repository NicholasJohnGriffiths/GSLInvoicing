using System.Security.Claims;
using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSLInvoicing.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;

    public AccountController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var appUser = await _context.AppUsers
            .AsNoTracking()
            .Include(u => u.Vendor)
            .FirstOrDefaultAsync(u => u.UserName == model.UserName);

        if (appUser == null || !string.Equals(appUser.Password, model.Password, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, appUser.UserName),
            new(ClaimTypes.NameIdentifier, appUser.Id.ToString()),
            new("VendorId", appUser.VendorId.ToString()),
            new("UserType", ((int)appUser.UserType).ToString())
        };

        if (!string.IsNullOrWhiteSpace(appUser.Vendor?.Name))
        {
            claims.Add(new Claim("VendorName", appUser.Vendor.Name));
        }

        if (appUser.UserType == UserType.Admin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl)
            && Uri.TryCreate(model.ReturnUrl, UriKind.Relative, out _)
            && model.ReturnUrl.StartsWith('/'))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return new RedirectToActionResult("Index", "Home", null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
