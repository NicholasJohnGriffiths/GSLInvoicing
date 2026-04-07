using System.Security.Claims;
using GSLInvoicing.Web.Controllers;
using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSLInvoicing.Web.Tests;

public class HomeAndAccountControllerPageTests
{
    [Fact]
    public void Home_Index_Returns_View()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);

        var result = controller.Index();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Home_Privacy_Returns_View()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Home_Error_Returns_View_With_ErrorModel()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<ErrorViewModel>(view.Model);
    }

    [Fact]
    public async Task Account_Login_Get_Returns_View_With_ReturnUrl()
    {
        await using var context = CreateContext();
        var controller = new AccountController(context);

        var result = controller.Login("/Invoices");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginViewModel>(view.Model);
        Assert.Equal("/Invoices", model.ReturnUrl);
    }

    [Fact]
    public async Task Account_Login_Post_Invalid_Model_Returns_View()
    {
        await using var context = CreateContext();
        var controller = new AccountController(context);
        controller.ModelState.AddModelError("UserName", "Required");

        var result = await controller.Login(new LoginViewModel
        {
            UserName = string.Empty,
            Password = string.Empty
        });

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Account_Login_Post_Valid_AppUser_Signs_In_With_Vendor_Claim()
    {
        await using var context = CreateContext();
        context.Vendors.Add(new Vendor { Id = 3, Name = "Vendor 3" });
        context.AppUsers.Add(new AppUser
        {
            UserName = "vendor.user",
            Password = "secret123",
            VendorId = 3,
            UserType = UserType.Admin
        });
        await context.SaveChangesAsync();

        var authService = new TestAuthenticationService();
        var controller = new AccountController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection()
                        .AddSingleton<IAuthenticationService>(authService)
                        .BuildServiceProvider()
                }
            }
        };

        var result = await controller.Login(new LoginViewModel
        {
            UserName = "vendor.user",
            Password = "secret123"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.NotNull(authService.LastPrincipal);
        Assert.Equal("vendor.user", authService.LastPrincipal!.Identity?.Name);
        Assert.Equal("3", authService.LastPrincipal.FindFirst("VendorId")?.Value);
        Assert.Equal("1", authService.LastPrincipal.FindFirst("UserType")?.Value);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"home-account-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? LastPrincipal { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            LastPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }
}
