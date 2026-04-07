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
    public async Task Export_Uses_Expected_Tsv_Headers()
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();

        context.Clients.Add(new Client
        {
            Id = 1,
            CardId = "1002",
            Name = "Aligned Client",
            Contact = "Aligned Contact",
            GSTCode = "S",
            Rate = 100m,
            DateCreated = currentDate
        });

        context.Invoices.Add(new Invoice
        {
            Id = 20,
            ClientId = 1,
            InvoiceNumber = "GSL2000",
            InvoiceDate = currentDate,
            DateCreated = currentDate,
            InvoiceItems =
            [
                new InvoiceItem
                {
                    Id = 200,
                    Description = "Aligned Line",
                    Hours = 1,
                    Rate = 100m,
                    Amount = 100m,
                    GST = 15m,
                    Total = 115m
                }
            ]
        });

        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        var actionResult = await controller.Export(currentDate.Year, currentDate.Month);

        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        Assert.False(
            fileResult.FileContents.Length >= 3
            && fileResult.FileContents[0] == 0xEF
            && fileResult.FileContents[1] == 0xBB
            && fileResult.FileContents[2] == 0xBF);
        var text = Encoding.UTF8.GetString(fileResult.FileContents);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split('\t');
        var firstRow = lines[1].Split('\t');

        Assert.Equal(headers.Length, firstRow.Length);
        Assert.Equal(
            [
                "Card ID",
                "Co./Last Name",
                "Delivery Status",
                "Invoice No.",
                "Item Number",
                "Quantity",
                "Customer PO",
                "Date",
                "Description",
                "Price",
                "Total",
                "GST Amount",
                "Inc-GST Total",
                "Comment",
                "Journal Memo",
                "GST Code",
                "Terms - Payment is Due",
                " - Balance Due Days"
            ],
            headers);
        Assert.Equal(currentDate.ToString("dd/MM/yyyy"), firstRow[7]);
        Assert.Equal("Sale; Aligned Client", firstRow[14]);
    }

    [Fact]
    public async Task Export_Inserts_Blank_Line_Between_Invoices()
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();

        context.Clients.Add(new Client
        {
            Id = 1,
            CardId = "1001",
            Name = "Blank Line Client",
            Contact = "Client Contact",
            GSTCode = "S",
            Rate = 120m,
            DateCreated = currentDate
        });

        context.Invoices.AddRange(
            new Invoice
            {
                Id = 30,
                ClientId = 1,
                InvoiceNumber = "GSL3000",
                InvoiceDate = currentDate,
                DateCreated = currentDate,
                InvoiceItems =
                [
                    new InvoiceItem
                    {
                        Id = 300,
                        Description = "Line 1",
                        Hours = 1,
                        Rate = 100m,
                        Amount = 100m,
                        GST = 15m,
                        Total = 115m
                    }
                ]
            },
            new Invoice
            {
                Id = 31,
                ClientId = 1,
                InvoiceNumber = "GSL3001",
                InvoiceDate = currentDate,
                DateCreated = currentDate,
                InvoiceItems =
                [
                    new InvoiceItem
                    {
                        Id = 301,
                        Description = "Line 2",
                        Hours = 1,
                        Rate = 200m,
                        Amount = 200m,
                        GST = 30m,
                        Total = 230m
                    }
                ]
            });

        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        var actionResult = await controller.Export(currentDate.Year, currentDate.Month);

        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        var text = Encoding.UTF8.GetString(fileResult.FileContents);

        Assert.Contains("GSL3000", text);
        Assert.Contains("GSL3001", text);
        Assert.Contains("\r\n\r\n1001\tBlank Line Client\tB\tGSL3001", text);
    }

    [Fact]
    public async Task Index_With_Vendor_Claim_Returns_Only_Current_Vendor_Invoices()
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();

        context.Clients.AddRange(
            new Client
            {
                Id = 1,
                Name = "Vendor One Client",
                VendorId = 1,
                GSTCode = "S",
                Rate = 120m,
                DateCreated = currentDate
            },
            new Client
            {
                Id = 2,
                Name = "Vendor Two Client",
                VendorId = 2,
                GSTCode = "S",
                Rate = 120m,
                DateCreated = currentDate
            });

        context.Invoices.AddRange(
            new Invoice
            {
                Id = 40,
                ClientId = 1,
                InvoiceNumber = "GSL4000",
                InvoiceDate = currentDate,
                DateCreated = currentDate
            },
            new Invoice
            {
                Id = 41,
                ClientId = 2,
                InvoiceNumber = "GSL4001",
                InvoiceDate = currentDate,
                DateCreated = currentDate
            });

        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim("VendorId", "1")
                ], "TestAuth"))
            }
        };

        var result = await controller.Index(currentDate.Year, currentDate.Month);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Invoice>>(view.Model).ToList();
        Assert.Single(model);
        Assert.Equal("GSL4000", model[0].InvoiceNumber);
    }

    [Fact]
    public async Task Print_Returns_Pdf_File_For_Invoice()
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();

        var vendor = new Vendor
        {
            Id = 1,
            Name = "Griffin Solutions Ltd",
            Address = "123 Test Street\nAuckland",
            Email = "accounts@example.com",
            Phone = "09 123 4567",
            GSTNumber = "GST-123"
        };

        var appUser = new AppUser
        {
            Id = 99,
            UserName = "print.user",
            Password = "password",
            Email = "print.user@example.com",
            Phone = "0800 555 111",
            VendorId = 1,
            UserType = UserType.Admin,
            Vendor = vendor
        };

        var client = new Client
        {
            Id = 1,
            VendorId = 1,
            CardId = "1001",
            Name = "Print Client",
            Contact = "Jane Client",
            GSTCode = "S",
            Rate = 120m,
            DateCreated = currentDate,
            Vendor = vendor
        };

        context.Vendors.Add(vendor);
        context.AppUsers.Add(appUser);
        context.Clients.Add(client);
        context.Invoices.Add(new Invoice
        {
            Id = 50,
            ClientId = 1,
            InvoiceNumber = "GSL5000",
            InvoiceDate = currentDate,
            PONumber = "PO-5000",
            Contact = "Jane Client",
            Notes = "Thanks for your business.",
            DateCreated = currentDate,
            Client = client,
            InvoiceItems =
            [
                new InvoiceItem
                {
                    Id = 500,
                    Description = "PDF line item",
                    Hours = 2,
                    Rate = 100m,
                    Amount = 200m,
                    GST = 30m,
                    Total = 230m
                }
            ]
        });

        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "99"),
                    new System.Security.Claims.Claim("VendorId", "1")
                ], "TestAuth"))
            }
        };

        var actionResult = await controller.Print(50);

        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("GSL5000.pdf", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 4);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(fileResult.FileContents, 0, 4));
    }

    [Fact]
    public async Task AddItem_With_Direct_Amount_Allows_Hours_And_Rate_To_Be_Ignored()
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();

        context.Clients.Add(new Client
        {
            Id = 1,
            Name = "Flat Fee Client",
            GSTCode = "S",
            Rate = 120m,
            DateCreated = currentDate
        });

        context.Invoices.Add(new Invoice
        {
            Id = 60,
            ClientId = 1,
            InvoiceNumber = "GSL6000",
            InvoiceDate = currentDate,
            DateCreated = currentDate
        });

        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        var result = await controller.AddItem(60, new AddInvoiceItemInput
        {
            Description = "Monthly retainer",
            Amount = 250m
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(InvoicesController.Edit), redirect.ActionName);

        var item = await context.InvoiceItems.SingleAsync();
        Assert.Equal("Monthly retainer", item.Description);
        Assert.Equal(0d, item.Hours);
        Assert.Equal(0m, item.Rate);
        Assert.Equal(250m, item.Amount);
        Assert.Equal(37.5m, item.GST);
        Assert.Equal(287.5m, item.Total);
    }

    [Fact]
    public async Task EditItem_With_Direct_Amount_Updates_Stored_Values()
    {
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        await using var context = CreateContext();

        context.Clients.Add(new Client
        {
            Id = 1,
            Name = "Update Client",
            GSTCode = "S",
            Rate = 120m,
            DateCreated = currentDate
        });

        context.Invoices.Add(new Invoice
        {
            Id = 61,
            ClientId = 1,
            InvoiceNumber = "GSL6001",
            InvoiceDate = currentDate,
            DateCreated = currentDate
        });

        context.InvoiceItems.Add(new InvoiceItem
        {
            Id = 610,
            InvoiceId = 61,
            Description = "Old item",
            Hours = 2,
            Rate = 100m,
            Amount = 200m,
            GST = 30m,
            Total = 230m
        });

        await context.SaveChangesAsync();

        var controller = new InvoicesController(context);
        var result = await controller.EditItem(61, 610, new AddInvoiceItemInput
        {
            Description = "Fixed fee",
            Amount = 300m
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(InvoicesController.Edit), redirect.ActionName);

        var item = await context.InvoiceItems.SingleAsync(ii => ii.Id == 610);
        Assert.Equal("Fixed fee", item.Description);
        Assert.Equal(0d, item.Hours);
        Assert.Equal(0m, item.Rate);
        Assert.Equal(300m, item.Amount);
        Assert.Equal(45m, item.GST);
        Assert.Equal(345m, item.Total);
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
            CardId = "1001",
            Name = "Test Client",
            Contact = "Client Contact",
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
        Assert.All(dataRows, row => Assert.StartsWith("1001\t", row));
        Assert.All(dataRows, row => Assert.Contains("\tB\t", row));
        Assert.All(dataRows, row => Assert.Contains("\tClient Contact\t", row));
        Assert.All(dataRows, row => Assert.Contains("GSL9999", row));
        Assert.DoesNotContain(dataRows, row => row.Contains("GSL9998"));
        Assert.DoesNotContain(dataRows, row => row.Contains("GSL9997"));
        Assert.Contains(dataRows, row => row.Contains("Line A (1 hrs)"));
        Assert.Contains(dataRows, row => row.Contains("Line B (2 hrs)"));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"invoice-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
