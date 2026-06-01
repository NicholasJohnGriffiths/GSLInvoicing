using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using GSLInvoicing.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Data;
using System.Security.Claims;

namespace GSLInvoicing.Web.Controllers;

public class InvoicesController : Controller
{
    private static readonly string[] InvoiceExportHeaders =
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
    ];

    private readonly AppDbContext _context;

    public InvoicesController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? year, int? month)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        if (selectedMonth is < 1 or > 12)
        {
            selectedMonth = today.Month;
        }

        var availableYears = await _context.Invoices
            .AsNoTracking()
            .Where(i => !vendorId.HasValue || i.Client.VendorId == vendorId.Value)
            .Select(i => i.InvoiceDate.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        if (!availableYears.Contains(selectedYear))
        {
            availableYears.Insert(0, selectedYear);
            availableYears = availableYears.Distinct().OrderByDescending(y => y).ToList();
        }

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .Include(i => i.InvoiceItems)
            .Where(i => (!vendorId.HasValue || i.Client.VendorId == vendorId.Value) && i.InvoiceDate.Year == selectedYear && i.InvoiceDate.Month == selectedMonth)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync();

        ViewBag.SelectedYear = selectedYear;
        ViewBag.SelectedMonth = selectedMonth;
        ViewBag.AvailableYears = availableYears;

        return View(invoices);
    }

    public async Task<IActionResult> Create()
    {
        var model = new InvoiceCreateViewModel
        {
            Clients = await GetClientsSelectList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Clients = await GetClientsSelectList();
            return View(model);
        }

        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var clientExists = await _context.Clients
            .AsNoTracking()
            .AnyAsync(c => c.Id == model.ClientId && (!vendorId.HasValue || c.VendorId == vendorId.Value));

        if (!clientExists)
        {
            ModelState.AddModelError(nameof(model.ClientId), "Please select a valid client.");
            model.Clients = await GetClientsSelectList();
            return View(model);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var config = await _context.Configs
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();

        if (config == null)
        {
            config = new Config { LastInvoiceNumber = "GSL0000" };
            _context.Configs.Add(config);
            await _context.SaveChangesAsync();
        }

        var nextInvoiceNumber = IncrementInvoiceNumber(config.LastInvoiceNumber);
        config.LastInvoiceNumber = nextInvoiceNumber;

        var invoice = new Invoice
        {
            ClientId = model.ClientId,
            InvoiceNumber = nextInvoiceNumber,
            InvoiceDate = model.InvoiceDate,
            PONumber = model.PONumber,
            Contact = model.Contact,
            Notes = model.Notes,
            DateCreated = DateOnly.FromDateTime(DateTime.Today)
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return RedirectToAction(nameof(Edit), new { id = invoice.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Export(int? year, int? month)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        if (selectedMonth is < 1 or > 12)
        {
            selectedMonth = today.Month;
        }

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .Include(i => i.InvoiceItems)
            .Where(i => (!vendorId.HasValue || i.Client.VendorId == vendorId.Value) && i.InvoiceDate.Year == selectedYear && i.InvoiceDate.Month == selectedMonth)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.Id)
            .ToListAsync();

        var tsv = new StringBuilder();
        tsv.AppendLine(string.Join('\t', InvoiceExportHeaders.Select(EscapeTsv)));

        foreach (var invoice in invoices)
        {
            var orderedItems = invoice.InvoiceItems.OrderBy(ii => ii.Id).ToList();
            if (orderedItems.Count == 0)
            {
                continue;
            }

            foreach (var item in orderedItems)
            {
                var row = BuildInvoiceExportRow(invoice, item);
                tsv.AppendLine(string.Join('\t', row.Select(EscapeTsv)));
            }

            tsv.AppendLine();
        }

        var fileName = $"invoices-export-{selectedYear:D4}-{selectedMonth:D2}.txt";
        var bytes = Encoding.UTF8.GetBytes(tsv.ToString());
        return File(bytes, "text/tab-separated-values", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Preview(int id)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .ThenInclude(c => c.Vendor)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id && (!vendorId.HasValue || i.Client.VendorId == vendorId.Value));

        if (invoice == null)
        {
            return NotFound();
        }

        AppUser? appUser = null;
        var appUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(appUserIdValue, out var appUserId))
        {
            appUser = await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == appUserId);
        }

        var pdfBytes = InvoicePdfGenerator.Generate(invoice, appUser);
        return File(pdfBytes, "application/pdf");
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .ThenInclude(c => c.Vendor)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id && (!vendorId.HasValue || i.Client.VendorId == vendorId.Value));

        if (invoice == null)
        {
            return NotFound();
        }

        AppUser? appUser = null;
        var appUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(appUserIdValue, out var appUserId))
        {
            appUser = await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == appUserId);
        }

        var pdfBytes = InvoicePdfGenerator.Generate(invoice, appUser);
        return File(pdfBytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id && (!vendorId.HasValue || i.Client.VendorId == vendorId.Value));

        if (invoice == null)
        {
            return NotFound();
        }

        var viewModel = await BuildInvoiceEditViewModel(invoice);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InvoiceEditViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id && (!vendorId.HasValue || i.Client.VendorId == vendorId.Value));
        if (invoice == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var vm = await BuildInvoiceEditViewModel(invoice, model);
            return View(vm);
        }

        invoice.ClientId = model.ClientId;
        invoice.InvoiceNumber = model.InvoiceNumber;
        invoice.InvoiceDate = model.InvoiceDate;
        invoice.PONumber = model.PONumber;
        invoice.Contact = model.Contact;
        invoice.Notes = model.Notes;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id = invoice.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(int id, AddInvoiceItemInput newItem)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id && (!vendorId.HasValue || i.Client.VendorId == vendorId.Value));

        if (invoice == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["InvoiceItemError"] = "Please enter either Amount or both Hours and Rate values.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var gstRate = GetGstRate(invoice.Client.GSTCode);
        var (hours, rate, amount) = ResolveLineValues(newItem);
        var gst = Math.Round(amount * gstRate, 2, MidpointRounding.AwayFromZero);
        var total = amount + gst;

        var item = new InvoiceItem
        {
            InvoiceId = id,
            Description = newItem.Description,
            Hours = hours,
            Rate = rate,
            Amount = amount,
            GST = gst,
            Total = total
        };

        _context.InvoiceItems.Add(item);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems)
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id && (!vendorId.HasValue || i.Client.VendorId == vendorId.Value));

        if (invoice == null)
        {
            return NotFound();
        }

        if (invoice.InvoiceItems.Count > 0)
        {
            _context.InvoiceItems.RemoveRange(invoice.InvoiceItems);
        }

        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, int itemId)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var item = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Id == itemId && ii.InvoiceId == id)
            .Join(_context.Invoices.Include(i => i.Client), ii => ii.InvoiceId, i => i.Id, (ii, i) => new { Item = ii, Invoice = i })
            .Where(x => !vendorId.HasValue || x.Invoice.Client.VendorId == vendorId.Value)
            .Select(x => x.Item)
            .FirstOrDefaultAsync();

        if (item == null)
        {
            return NotFound();
        }

        _context.InvoiceItems.Remove(new InvoiceItem { Id = itemId });
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditItem(int id, int itemId, AddInvoiceItemInput updatedItem)
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id && (!vendorId.HasValue || i.Client.VendorId == vendorId.Value));

        if (invoice == null)
        {
            return NotFound();
        }

        var item = await _context.InvoiceItems
            .FirstOrDefaultAsync(ii => ii.Id == itemId && ii.InvoiceId == id);

        if (item == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["InvoiceItemError"] = "Please enter either Amount or both Hours and Rate values.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var gstRate = GetGstRate(invoice.Client.GSTCode);
        var (hours, rate, amount) = ResolveLineValues(updatedItem);
        var gst = Math.Round(amount * gstRate, 2, MidpointRounding.AwayFromZero);
        var total = amount + gst;

        item.Description = updatedItem.Description;
        item.Hours = hours;
        item.Rate = rate;
        item.Amount = amount;
        item.GST = gst;
        item.Total = total;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<InvoiceEditViewModel> BuildInvoiceEditViewModel(Invoice invoice, InvoiceEditViewModel? fallback = null)
    {
        var invoiceWithItems = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Client)
            .Include(i => i.InvoiceItems)
            .FirstAsync(i => i.Id == invoice.Id);

        var vendorId = GetCurrentVendorId();

        var clients = await _context.Clients
            .AsNoTracking()
            .Where(c => !vendorId.HasValue || c.VendorId == vendorId.Value)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();

        var currentCode = invoiceWithItems.Client.GSTCode;

        return new InvoiceEditViewModel
        {
            Id = invoiceWithItems.Id,
            ClientId = fallback?.ClientId ?? invoiceWithItems.ClientId,
            InvoiceNumber = fallback?.InvoiceNumber ?? invoiceWithItems.InvoiceNumber,
            InvoiceDate = fallback?.InvoiceDate ?? invoiceWithItems.InvoiceDate,
            PONumber = fallback?.PONumber ?? invoiceWithItems.PONumber,
            Contact = fallback?.Contact ?? invoiceWithItems.Contact,
            Notes = fallback?.Notes ?? invoiceWithItems.Notes,
            ClientGstCode = currentCode,
            GstRatePercent = GetGstRate(currentCode) * 100,
            Clients = clients,
            Items = invoiceWithItems.InvoiceItems
                .OrderBy(ii => ii.Id)
                .Select(ii => new InvoiceItemDisplayViewModel
                {
                    Id = ii.Id,
                    Description = ii.Description,
                    Hours = ii.Hours,
                    Rate = ii.Rate,
                    Amount = ii.Amount,
                    GST = ii.GST,
                    Total = ii.Total
                })
                .ToList(),
            NewItem = fallback?.NewItem ?? new AddInvoiceItemInput
            {
                Rate = invoiceWithItems.Client.Rate
            }
        };
    }

    private static (double Hours, decimal Rate, decimal Amount) ResolveLineValues(AddInvoiceItemInput item)
    {
        var hasHoursAndRate = item.Hours.HasValue && item.Hours.Value > 0 && item.Rate.HasValue && item.Rate.Value > 0;
        if (hasHoursAndRate)
        {
            var hours = item.Hours!.Value;
            var rate = item.Rate!.Value;
            var calculatedAmount = Math.Round(rate * (decimal)hours, 2, MidpointRounding.AwayFromZero);
            return (hours, rate, calculatedAmount);
        }

        var amount = Math.Round(item.Amount ?? 0m, 2, MidpointRounding.AwayFromZero);
        return (0d, 0m, amount);
    }

    private static decimal GetGstRate(string? gstCode)
    {
        return gstCode?.Trim().ToUpperInvariant() switch
        {
            "S" => 0.15m,
            "Z" => 0m,
            "N-T" => 0m,
            _ => 0m
        };
    }

    private async Task<List<SelectListItem>> GetClientsSelectList()
    {
        var vendorId = GetCurrentVendorId();
        if (vendorId == null && IsAuthenticatedUser())
        {
            return [];
        }

        return await _context.Clients
            .AsNoTracking()
            .Where(c => !vendorId.HasValue || c.VendorId == vendorId.Value)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }

    private int? GetCurrentVendorId()
    {
        var claimValue = ControllerContext?.HttpContext?.User?.FindFirst("VendorId")?.Value;
        return int.TryParse(claimValue, out var vendorId) ? vendorId : null;
    }

    private bool IsAuthenticatedUser()
    {
        return ControllerContext?.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }

    private static string IncrementInvoiceNumber(string? lastInvoiceNumber)
    {
        var value = (lastInvoiceNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "GSL0001";
        }

        var splitIndex = value.Length;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(value[i]))
            {
                splitIndex = i + 1;
                break;
            }

            if (i == 0)
            {
                splitIndex = 0;
            }
        }

        var prefix = value[..splitIndex];
        var numericPart = value[splitIndex..];

        if (numericPart.Length == 0)
        {
            return prefix + "0001";
        }

        if (!int.TryParse(numericPart, out var number))
        {
            return prefix + "0001";
        }

        var incremented = number + 1;
        return prefix + incremented.ToString($"D{numericPart.Length}");
    }

    private static IEnumerable<string> BuildInvoiceExportRow(Invoice invoice, InvoiceItem? item)
    {
        var amount = item?.Amount ?? 0m;
        var gst = item?.GST ?? 0m;
        var incGstTotal = amount + gst;
        var description = item == null
            ? string.Empty
            : item.Hours > 0
                ? $"{item.Description} ({item.Hours:0.##} hrs)"
                : item.Description ?? string.Empty;

        return
        [
            invoice.Client?.CardId ?? invoice.ClientId.ToString(),
            invoice.Client?.Name ?? string.Empty,
            "B",
            invoice.InvoiceNumber,
            "MISC",
            "1",
            invoice.PONumber ?? string.Empty,
            invoice.InvoiceDate.ToString("dd/MM/yyyy"),
            description,
            amount.ToString("0.00"),
            amount.ToString("0.00"),
            gst.ToString("0.00"),
            incGstTotal.ToString("0.00"),
            invoice.Client?.Contact ?? string.Empty,
            invoice.Client is null ? string.Empty : $"Sale; {invoice.Client.Name}",
            invoice.Client?.GSTCode ?? string.Empty,
            "4",
            "20"
        ];
    }

    private static string EscapeTsv(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\t", " ")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }
}
