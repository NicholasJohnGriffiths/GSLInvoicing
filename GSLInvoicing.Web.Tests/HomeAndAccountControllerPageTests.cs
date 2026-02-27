using GSLInvoicing.Web.Controllers;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public void Account_Login_Get_Returns_View_With_ReturnUrl()
    {
        var controller = new AccountController();

        var result = controller.Login("/Invoices");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginViewModel>(view.Model);
        Assert.Equal("/Invoices", model.ReturnUrl);
    }

    [Fact]
    public async Task Account_Login_Post_Invalid_Model_Returns_View()
    {
        var controller = new AccountController();
        controller.ModelState.AddModelError("Email", "Required");

        var result = await controller.Login(new LoginViewModel
        {
            Email = string.Empty,
            Password = string.Empty
        });

        Assert.IsType<ViewResult>(result);
    }
}
