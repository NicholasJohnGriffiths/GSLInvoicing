using System.Text;
using GSLInvoicing.Web.Controllers;
using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GSLInvoicing.Web.Tests;

public class InvoicesControllerPageTests
{
    [Fact]
    public async Task Index_Returns_View_With_Filtered_Invoices()
    {
        var current = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();
        var client = new Client
        {
            Id = 1,
            Name = "Index Client",
            GSTCode = "S",
            Rate = 120m,
            DateCreated = current
        };

        context.Clients.Add(client);
        context.Invoices.AddRange(
            new Invoice
            {
                Id = 1,
                ClientId = 1,
                InvoiceNumber = "GSL1001",
                InvoiceDate = current,
                DateCreated = current
            },
            new Invoice
            {
                Id = 2,
                ClientId = 1,
                InvoiceNumber = "GSL1002",
                InvoiceDate = current.AddMonths(-1),
                DateCreated = current.AddMonths(-1)
            });

        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        var result = await controller.Index(current.Year, current.Month);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Invoice>>(view.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Create_Get_Returns_ViewModel_With_Client_List()
    {
        var current = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();
        context.Clients.Add(new Client
        {
            Id = 1,
            Name = "Create Client",
            GSTCode = "S",
            Rate = 100m,
            DateCreated = current
        });
        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        var result = await controller.Create();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InvoiceCreateViewModel>(view.Model);
        Assert.Single(model.Clients);
    }

    [Fact]
    public async Task Edit_Get_Returns_ViewModel_With_Prefilled_NewItem_Rate()
    {
        var current = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();
        context.Clients.Add(new Client
        {
            Id = 1,
            Name = "Edit Client",
            GSTCode = "S",
            Rate = 245m,
            DateCreated = current
        });
        context.Invoices.Add(new Invoice
        {
            Id = 10,
            ClientId = 1,
            InvoiceNumber = "GSL2001",
            InvoiceDate = current,
            DateCreated = current
        });
        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        var result = await controller.Edit(10);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InvoiceEditViewModel>(view.Model);
        Assert.Equal(245m, model.NewItem.Rate);
    }

    [Fact]
    public async Task Edit_Get_Unknown_Id_Returns_NotFound()
    {
        await using var context = CreateContext();
        var controller = new InvoicesController(context);

        var result = await controller.Edit(9999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Export_Writes_One_Data_Row_Per_Invoice_Item()
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"invoice-export-{Guid.NewGuid()}")
            .Options;

        await using var context = new AppDbContext(options);

        var client = new Client
        {
            Id = 1,
            Name = "Test Client",
            GSTCode = "S",
            Rate = 120m,
            DateCreated = currentDate
        };

        var invoiceWithItems = new Invoice
        {
            Id = 10,
            ClientId = client.Id,
            InvoiceNumber = "GSL9999",
            InvoiceDate = currentDate,
            DateCreated = currentDate
        };

        var invoiceWithoutItems = new Invoice
        {
            Id = 11,
            ClientId = client.Id,
            InvoiceNumber = "GSL9998",
            InvoiceDate = currentDate,
            DateCreated = currentDate
        };

        var outOfMonthInvoice = new Invoice
        {
            Id = 12,
            ClientId = client.Id,
            InvoiceNumber = "GSL9997",
            InvoiceDate = currentDate.AddMonths(-1),
            DateCreated = currentDate.AddMonths(-1)
        };

        var item1 = new InvoiceItem
        {
            Id = 100,
            InvoiceId = invoiceWithItems.Id,
            Description = "Line A",
            Hours = 1,
            Rate = 100m,
            Amount = 100m,
            GST = 15m,
            Total = 115m
        };

        var item2 = new InvoiceItem
        {
            Id = 101,
            InvoiceId = invoiceWithItems.Id,
            Description = "Line B",
            Hours = 2,
            Rate = 50m,
            Amount = 100m,
            GST = 15m,
            Total = 115m
        };

        var outOfMonthItem = new InvoiceItem
        {
            Id = 102,
            InvoiceId = outOfMonthInvoice.Id,
            Description = "Out Of Month",
            Hours = 1,
            Rate = 10m,
            Amount = 10m,
            GST = 1.5m,
            Total = 11.5m
        };

        context.Clients.Add(client);
        context.Invoices.AddRange(invoiceWithItems, invoiceWithoutItems, outOfMonthInvoice);
        context.InvoiceItems.AddRange(item1, item2, outOfMonthItem);
        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);

        var actionResult = await controller.Export(currentDate.Year, currentDate.Month);

        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        var text = Encoding.UTF8.GetString(fileResult.FileContents);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var lines = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);

        var dataRows = lines.Skip(1).ToList();
        Assert.All(dataRows, row => Assert.Contains("GSL9999", row));
        Assert.DoesNotContain(dataRows, row => row.Contains("GSL9998"));
        Assert.DoesNotContain(dataRows, row => row.Contains("GSL9997"));
        Assert.Contains(dataRows, row => row.Contains("Line A"));
        Assert.Contains(dataRows, row => row.Contains("Line B"));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"invoice-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
