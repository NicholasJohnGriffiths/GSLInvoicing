using System.Security.Claims;
using System.Text;
using GSLInvoicing.Web.Controllers;
using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Http;
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
    public async Task Index_With_Vendor_Claim_Returns_Only_Current_Vendor_Clients()
    {
        await using var context = CreateContext();
        context.Clients.AddRange(
            new Client
            {
                Name = "Vendor One Client",
                VendorId = 1,
                Rate = 100m,
                DateCreated = DateOnly.FromDateTime(DateTime.Today)
            },
            new Client
            {
                Name = "Vendor Two Client",
                VendorId = 2,
                Rate = 100m,
                DateCreated = DateOnly.FromDateTime(DateTime.Today)
            });
        await context.SaveChangesAsync();

        var controller = new ClientsController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("VendorId", "1")
                ], "TestAuth"))
            }
        };

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Client>>(view.Model).ToList();
        Assert.Single(model);
        Assert.Equal("Vendor One Client", model[0].Name);
    }

    [Fact]
    public async Task Index_As_Admin_With_Filter_Returns_Selected_Vendor_Clients()
    {
        await using var context = CreateContext();
        context.Vendors.AddRange(
            new Vendor { Id = 1, Name = "Vendor One" },
            new Vendor { Id = 2, Name = "Vendor Two" });
        context.Clients.AddRange(
            new Client
            {
                Name = "Vendor One Client",
                VendorId = 1,
                Rate = 100m,
                DateCreated = DateOnly.FromDateTime(DateTime.Today)
            },
            new Client
            {
                Name = "Vendor Two Client",
                VendorId = 2,
                Rate = 100m,
                DateCreated = DateOnly.FromDateTime(DateTime.Today)
            });
        await context.SaveChangesAsync();

        var controller = new ClientsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("VendorId", "1"),
                        new Claim("UserType", "1")
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.Index(2);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Client>>(view.Model).ToList();
        Assert.Single(model);
        Assert.Equal("Vendor Two Client", model[0].Name);
        Assert.True((bool)view.ViewData["AllowVendorFilter"]!);
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
    public async Task Create_Get_As_Admin_With_Single_Vendor_AutoSelects_And_Locks_Selector()
    {
        await using var context = CreateContext();
        context.Vendors.Add(new Vendor { Id = 9, Name = "Only Vendor" });
        await context.SaveChangesAsync();

        var controller = new ClientsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("VendorId", "9"),
                        new Claim("UserType", "1")
                    ], "TestAuth"))
                }
            }
        };

        var result = controller.Create();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Client>(view.Model);
        Assert.Equal(9, model.VendorId);
        Assert.True((bool)view.ViewData["AllowVendorSelection"]!);
        Assert.True((bool)view.ViewData["IsVendorSelectionLocked"]!);
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
        Assert.False(
            file.FileContents.Length >= 3
            && file.FileContents[0] == 0xEF
            && file.FileContents[1] == 0xBB
            && file.FileContents[2] == 0xBF);
        var text = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Co./Last Name", text);
        Assert.Contains("Export Client", text);
    }

    [Fact]
    public async Task Create_Post_As_Admin_Uses_Selected_Vendor()
    {
        await using var context = CreateContext();
        context.Vendors.AddRange(
            new Vendor { Id = 1, Name = "Vendor One" },
            new Vendor { Id = 2, Name = "Vendor Two" });
        await context.SaveChangesAsync();

        var controller = new ClientsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("VendorId", "1"),
                        new Claim("UserType", "1")
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.Create(new Client
        {
            Name = "Admin Created Client",
            VendorId = 2,
            GSTCode = "S",
            Rate = 90m,
            DateCreated = DateOnly.FromDateTime(DateTime.Today)
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ClientsController.Index), redirect.ActionName);

        var saved = await context.Clients.SingleAsync(c => c.Name == "Admin Created Client");
        Assert.Equal(2, saved.VendorId);
    }

    [Fact]
    public async Task Create_Post_As_Non_Admin_Uses_Claim_Vendor()
    {
        await using var context = CreateContext();
        context.Vendors.AddRange(
            new Vendor { Id = 1, Name = "Vendor One" },
            new Vendor { Id = 2, Name = "Vendor Two" });
        await context.SaveChangesAsync();

        var controller = new ClientsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("VendorId", "1"),
                        new Claim("UserType", "0")
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.Create(new Client
        {
            Name = "General User Client",
            VendorId = 2,
            GSTCode = "S",
            Rate = 120m,
            DateCreated = DateOnly.FromDateTime(DateTime.Today)
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ClientsController.Index), redirect.ActionName);

        var saved = await context.Clients.SingleAsync(c => c.Name == "General User Client");
        Assert.Equal(1, saved.VendorId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"clients-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
