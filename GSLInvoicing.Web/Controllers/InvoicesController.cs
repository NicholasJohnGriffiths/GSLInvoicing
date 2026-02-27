using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Data;

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
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        if (selectedMonth is < 1 or > 12)
        {
            selectedMonth = today.Month;
        }

        var availableYears = await _context.Invoices
            .AsNoTracking()
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
            .Where(i => i.InvoiceDate.Year == selectedYear && i.InvoiceDate.Month == selectedMonth)
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
            .Where(i => i.InvoiceDate.Year == selectedYear && i.InvoiceDate.Month == selectedMonth)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.Id)
            .ToListAsync();

        var tsv = new StringBuilder();
        tsv.AppendLine(string.Join('\t', InvoiceExportHeaders.Select(EscapeTsv)));

        foreach (var invoice in invoices)
        {
            foreach (var item in invoice.InvoiceItems.OrderBy(ii => ii.Id))
            {
                var row = BuildInvoiceExportRow(invoice, item);
                tsv.AppendLine(string.Join('\t', row.Select(EscapeTsv)));
            }
        }

        var fileName = $"invoices-export-{selectedYear:D4}-{selectedMonth:D2}.txt";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(tsv.ToString())).ToArray();
        return File(bytes, "text/tab-separated-values", fileName);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id);

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

        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
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
        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["InvoiceItemError"] = "Please enter valid Hours and Rate values.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var gstRate = GetGstRate(invoice.Client.GSTCode);
        var amount = Math.Round(newItem.Rate * (decimal)newItem.Hours, 2, MidpointRounding.AwayFromZero);
        var gst = Math.Round(amount * gstRate, 2, MidpointRounding.AwayFromZero);
        var total = amount + gst;

        var item = new InvoiceItem
        {
            InvoiceId = id,
            Description = newItem.Description,
            Hours = newItem.Hours,
            Rate = newItem.Rate,
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
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id);

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
        var item = await _context.InvoiceItems
            .AsNoTracking()
            .FirstOrDefaultAsync(ii => ii.Id == itemId && ii.InvoiceId == id);

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
        var invoice = await _context.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == id);

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
            TempData["InvoiceItemError"] = "Please enter valid Hours and Rate values.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var gstRate = GetGstRate(invoice.Client.GSTCode);
        var amount = Math.Round(updatedItem.Rate * (decimal)updatedItem.Hours, 2, MidpointRounding.AwayFromZero);
        var gst = Math.Round(amount * gstRate, 2, MidpointRounding.AwayFromZero);
        var total = amount + gst;

        item.Description = updatedItem.Description;
        item.Hours = updatedItem.Hours;
        item.Rate = updatedItem.Rate;
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

        var clients = await _context.Clients
            .AsNoTracking()
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

    private static decimal GetGstRate(string? gstCode)
    {
        return string.Equals(gstCode?.Trim(), "S", StringComparison.OrdinalIgnoreCase) ? 0.15m : 0m;
    }

    private async Task<List<SelectListItem>> GetClientsSelectList()
    {
        return await _context.Clients
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
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
            : $"{item.Description} ({item.Hours:0.##} hrs)";

        return
        [
            invoice.Client?.CardId ?? invoice.ClientId.ToString(),
            invoice.Client?.Name ?? string.Empty,
            "B",
            invoice.InvoiceNumber,
            "MISC",
            "1",
            invoice.PONumber ?? string.Empty,
            invoice.InvoiceDate.ToString("yyyy-MM-dd"),
            description,
            amount.ToString("0.00"),
            amount.ToString("0.00"),
            gst.ToString("0.00"),
            incGstTotal.ToString("0.00"),
            invoice.Client?.Contact ?? string.Empty,
            invoice.Client?.Name ?? string.Empty,
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
