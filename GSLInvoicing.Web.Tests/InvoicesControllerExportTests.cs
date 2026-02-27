using System.Text;
using GSLInvoicing.Web.Controllers;
using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GSLInvoicing.Web.Tests;

public class InvoicesControllerExportTests
{
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
}
