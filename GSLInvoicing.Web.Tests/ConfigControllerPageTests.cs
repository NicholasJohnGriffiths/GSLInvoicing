using GSLInvoicing.Web.Controllers;
using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GSLInvoicing.Web.Tests;

public class ConfigControllerPageTests
{
    [Fact]
    public async Task Index_Creates_Default_Config_When_Missing()
    {
        await using var context = CreateContext();
        var controller = new ConfigController(context);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Config>(view.Model);
        Assert.Equal("GSL0000", model.LastInvoiceNumber);
    }

    [Fact]
    public async Task Edit_Get_With_Wrong_Id_Redirects_To_Single_Config()
    {
        await using var context = CreateContext();
        context.Configs.Add(new Config { Id = 5, LastInvoiceNumber = "GSL0123" });
        await context.SaveChangesAsync();

        var controller = new ConfigController(context);
        var result = await controller.Edit(99);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(5, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Edit_Get_With_Correct_Id_Returns_View()
    {
        await using var context = CreateContext();
        context.Configs.Add(new Config { Id = 2, LastInvoiceNumber = "GSL0999" });
        await context.SaveChangesAsync();

        var controller = new ConfigController(context);
        var result = await controller.Edit(2);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Config>(view.Model);
        Assert.Equal("GSL0999", model.LastInvoiceNumber);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"config-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
