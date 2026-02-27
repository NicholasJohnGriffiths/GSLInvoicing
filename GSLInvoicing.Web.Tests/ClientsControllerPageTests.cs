using System.Text;
using GSLInvoicing.Web.Controllers;
using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GSLInvoicing.Web.Tests;

public class ClientsControllerPageTests
{
    [Fact]
    public async Task Index_Returns_View_With_Clients()
    {
        await using var context = CreateContext();
        context.Clients.Add(new Client
        {
            Name = "Acme",
            Rate = 100m,
            DateCreated = DateOnly.FromDateTime(DateTime.Today)
        });
        await context.SaveChangesAsync();

        var controller = new ClientsController(context);
        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Client>>(view.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Create_Get_Returns_View()
    {
        await using var context = CreateContext();
        var controller = new ClientsController(context);

        var result = controller.Create();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<Client>(view.Model);
    }

    [Fact]
    public async Task Details_Get_With_Null_Id_Returns_NotFound()
    {
        await using var context = CreateContext();
        var controller = new ClientsController(context);

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Get_With_Unknown_Id_Returns_NotFound()
    {
        await using var context = CreateContext();
        var controller = new ClientsController(context);

        var result = await controller.Edit(9999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_Get_With_Unknown_Id_Returns_NotFound()
    {
        await using var context = CreateContext();
        var controller = new ClientsController(context);

        var result = await controller.Delete(9999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ExportMyob_Returns_Tab_Delimited_File()
    {
        await using var context = CreateContext();
        context.Clients.Add(new Client
        {
            Id = 7,
            Name = "Export Client",
            Contact = "Test Contact",
            Email = "test@example.com",
            GSTCode = "S",
            Rate = 150m,
            DateCreated = DateOnly.FromDateTime(DateTime.Today)
        });
        await context.SaveChangesAsync();

        var controller = new ClientsController(context);

        var result = await controller.ExportMyob();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/tab-separated-values", file.ContentType);
        var text = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Co./Last Name", text);
        Assert.Contains("Export Client", text);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"clients-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
