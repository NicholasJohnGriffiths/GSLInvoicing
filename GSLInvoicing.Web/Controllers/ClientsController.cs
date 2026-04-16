using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Text;

namespace GSLInvoicing.Web.Controllers;

public class ClientsController : Controller
{
    private static readonly string[] MyobHeaders =
    [
        "Co./Last Name", "First Name", "Card ID", "Card Status", "Addr 1 - Line 1", "Addr 1 - Line 2", "Addr 1 - Line 3", "Addr 1 - Line 4", "Addr 1 - City", "Addr 1 - State", "Addr 1 - Postcode", "Addr 1 - Country", "Addr 1 - Phone  No. 1", "Addr 1 - Phone  No. 2", "Addr 1 - Phone  No. 3", "Addr 1 - Fax  No", "Addr 1 - Email", "Addr 1 - WWW", "Addr 1 - Contact Name", "Addr 1 - Salutation",
        "Addr 2 - Line 1", "Addr 2 - Line 2", "Addr 2 - Line 3", "Addr 2 - Line 4", "Addr 2 - City", "Addr 2 - State", "Addr 2 - Postcode", "Addr 2 - Country", "Addr 2 - Phone  No. 1", "Addr 2 - Phone  No. 2", "Addr 2 - Phone  No. 3", "Addr 2 - Fax  No", "Addr 2 - Email", "Addr 2 - WWW", "Addr 2 - Contact Name", "Addr 2 - Salutation",
        "Addr 3 - Line 1", "Addr 3 - Line 2", "Addr 3 - Line 3", "Addr 3 - Line 4", "Addr 3 - City", "Addr 3 - State", "Addr 3 - Postcode", "Addr 3 - Country", "Addr 3 - Phone  No. 1", "Addr 3 - Phone  No. 2", "Addr 3 - Phone  No. 3", "Addr 3 - Fax  No", "Addr 3 - Email", "Addr 3 - WWW", "Addr 3 - Contact Name", "Addr 3 - Salutation",
        "Addr 4 - Line 1", "Addr 4 - Line 2", "Addr 4 - Line 3", "Addr 4 - Line 4", "Addr 4 - City", "Addr 4 - State", "Addr 4 - Postcode", "Addr 4 - Country", "Addr 4 - Phone  No. 1", "Addr 4 - Phone  No. 2", "Addr 4 - Phone  No. 3", "Addr 4 - Fax  No", "Addr 4 - Email", "Addr 4 - WWW", "Addr 4 - Contact Name", "Addr 4 - Salutation",
        "Addr 5 - Line 1", "Addr 5 - Line 2", "Addr 5 - Line 3", "Addr 5 - Line 4", "Addr 5 - City", "Addr 5 - State", "Addr 5 - Postcode", "Addr 5 - Country", "Addr 5 - Phone  No. 1", "Addr 5 - Phone  No. 2", "Addr 5 - Phone  No. 3", "Addr 5 - Fax  No", "Addr 5 - Email", "Addr 5 - WWW", "Addr 5 - Contact Name", "Addr 5 - Salutation",
        "Picture", "Notes", "Identifiers", "Custom List 1", "Custom List 2", "Custom List 3", "Custom Field 1", "Custom Field 2", "Custom Field 3", "Terms - Payment is Due", "Terms - Discount Days", "Terms - Balance Due Days", "Terms - % Discount", "Terms - % Monthly Charge", "GST Code", "Credit Limit", "GST ID No.", "Volume Discount %", "Sales/Purchase Layout", "Payment Method", "Payment Notes", "Name on Card", "Card Number", "Expiry Date", "Bank and Branch", "Account Number", "Account Name", "Account", "Salesperson", "Salesperson Card ID", "Comment", "Shipping Method", "Printed Form", "Freight GST Code", "Use Customer's GST Code", "Receipt Memo", "Invoice/Purchase Order Delivery", "RecordID"
    ];

    private readonly AppDbContext _context;

    public ClientsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? vendorId = null)
    {
        var isAdmin = IsAdminUser();
        var currentVendorId = GetCurrentVendorId();

        if (!isAdmin && currentVendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        IQueryable<Client> query = _context.Clients.AsNoTracking();

        if (isAdmin)
        {
            query = query.Include(c => c.Vendor);

            var vendorOptions = await _context.Vendors
                .AsNoTracking()
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem
                {
                    Value = v.Id.ToString(),
                    Text = v.Name
                })
                .ToListAsync();

            ViewBag.AllowVendorFilter = true;
            ViewBag.VendorFilterOptions = vendorOptions;
            ViewBag.SelectedVendorId = vendorId;

            if (vendorId.HasValue && vendorId.Value > 0)
            {
                query = query.Where(c => c.VendorId == vendorId.Value);
            }
        }
        else if (currentVendorId.HasValue)
        {
            query = query.Where(c => c.VendorId == currentVendorId.Value);
        }

        var clients = await query
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(clients);
    }

    [HttpGet]
    public async Task<IActionResult> ExportMyob(int? vendorId = null)
    {
        var isAdmin = IsAdminUser();
        var currentVendorId = GetCurrentVendorId();

        if (!isAdmin && currentVendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        IQueryable<Client> query = _context.Clients.AsNoTracking();
        if (isAdmin)
        {
            if (vendorId.HasValue && vendorId.Value > 0)
            {
                query = query.Where(c => c.VendorId == vendorId.Value);
            }
        }
        else if (currentVendorId.HasValue)
        {
            query = query.Where(c => c.VendorId == currentVendorId.Value);
        }

        var clients = await query
            .OrderBy(c => c.Name)
            .ToListAsync();

        var tsv = new StringBuilder();
        tsv.AppendLine(string.Join('\t', MyobHeaders.Select(EscapeTsv)));

        foreach (var client in clients)
        {
            var mappedValues = new Dictionary<string, string?>
            {
                ["Co./Last Name"] = client.Name,
                ["Card ID"] = client.CardId ?? client.Id.ToString(),
                ["Card Status"] = "N",
                ["Addr 1 - Line 1"] = client.Street,
                ["Addr 1 - Line 2"] = client.Suburb,
                ["Addr 1 - City"] = client.City,
                ["Addr 1 - Postcode"] = client.Postcode,
                ["Addr 1 - Country"] = client.Country,
                ["Addr 1 - Email"] = client.Email,
                ["Addr 1 - Contact Name"] = client.Contact,
                ["GST Code"] = client.GSTCode,
                ["Terms - Payment is Due"] = "20",
                ["Credit Limit"] = client.Rate.ToString("0.00"),
                ["GST ID No."] = "Online Banking",
                ["Account"] = "41100",
                ["Name on Card"] = client.Name,
                ["Comment"] = $"Exported {client.DateCreated:yyyy-MM-dd}",
                ["RecordID"] = client.Id.ToString()
            };

            var row = MyobHeaders
                .Select(header => mappedValues.TryGetValue(header, out var value) ? value ?? string.Empty : string.Empty)
                .Select(EscapeTsv);

            tsv.AppendLine(string.Join('\t', row));
        }

        var fileName = $"clients-myob-{DateTime.Now:yyyyMMdd}.txt";
        var bytes = Encoding.UTF8.GetBytes(tsv.ToString());
        return File(bytes, "text/tab-separated-values", fileName);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();
        if (!isAdmin && vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || !vendorId.HasValue || m.VendorId == vendorId.Value));

        if (client == null)
        {
            return NotFound();
        }

        return View(client);
    }

    public IActionResult Create()
    {
        var vendorId = GetCurrentVendorId();
        var client = new Client
        {
            DateCreated = DateOnly.FromDateTime(DateTime.Today),
            VendorId = vendorId ?? 0
        };

        return CreateViewWithVendors(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Contact,Email,TransactionReference,BankAccount,Notes,GSTCode,Rate,DateCreated,Street,Suburb,City,Postcode,Country,VendorId")] Client client)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();

        if (!isAdmin && vendorId == null)
        {
            return Forbid();
        }

        if (isAdmin)
        {
            var adminVendorIds = await _context.Vendors
                .AsNoTracking()
                .OrderBy(v => v.Name)
                .Select(v => v.Id)
                .ToListAsync();

            if (adminVendorIds.Count == 1)
            {
                client.VendorId = adminVendorIds[0];
            }

            if (client.VendorId <= 0)
            {
                ModelState.AddModelError(nameof(Client.VendorId), "Vendor is required.");
            }
            else if (!adminVendorIds.Contains(client.VendorId))
            {
                ModelState.AddModelError(nameof(Client.VendorId), "Selected vendor was not found.");
            }
        }
        else
        {
            client.VendorId = vendorId!.Value;
        }

        if (!ModelState.IsValid)
        {
            return CreateViewWithVendors(client);
        }

        try
        {
            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            }

            var config = await _context.Configs
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                config = new Config
                {
                    LastInvoiceNumber = "GSL0000",
                    LastCardId = "0"
                };

                _context.Configs.Add(config);
                await _context.SaveChangesAsync();
            }

            var nextCardId = IncrementCardId(config.LastCardId);
            config.LastCardId = nextCardId;
            client.CardId = nextCardId;

            _context.Add(client);
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Unable to save client right now. Please check the form values and try again.");
            return View(client);
        }
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();
        if (!isAdmin && vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && (isAdmin || !vendorId.HasValue || c.VendorId == vendorId.Value));
        if (client == null)
        {
            return NotFound();
        }

        return CreateViewWithVendors(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,CardId,Name,Contact,Email,TransactionReference,BankAccount,Notes,GSTCode,Rate,DateCreated,Street,Suburb,City,Postcode,Country,VendorId")] Client client)
    {
        if (id != client.Id)
        {
            return NotFound();
        }

        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();

        if (!isAdmin && vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        if (isAdmin)
        {
            var adminVendorIds = await _context.Vendors
                .AsNoTracking()
                .OrderBy(v => v.Name)
                .Select(v => v.Id)
                .ToListAsync();

            if (adminVendorIds.Count == 1)
            {
                client.VendorId = adminVendorIds[0];
            }

            if (client.VendorId <= 0)
            {
                ModelState.AddModelError(nameof(Client.VendorId), "Vendor is required.");
            }
            else if (!adminVendorIds.Contains(client.VendorId))
            {
                ModelState.AddModelError(nameof(Client.VendorId), "Selected vendor was not found.");
            }
        }

        if (!ModelState.IsValid)
        {
            if (!isAdmin && vendorId.HasValue)
            {
                client.VendorId = vendorId.Value;
            }

            return CreateViewWithVendors(client);
        }

        try
        {
            var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && (isAdmin || !vendorId.HasValue || c.VendorId == vendorId.Value));
            if (existingClient == null)
            {
                return NotFound();
            }

            existingClient.CardId = client.CardId;
            existingClient.Name = client.Name;
            existingClient.Contact = client.Contact;
            existingClient.Email = client.Email;
            existingClient.TransactionReference = client.TransactionReference;
            existingClient.BankAccount = client.BankAccount;
            existingClient.Notes = client.Notes;
            existingClient.GSTCode = client.GSTCode;
            existingClient.Rate = client.Rate;
            existingClient.DateCreated = client.DateCreated;
            existingClient.Street = client.Street;
            existingClient.Suburb = client.Suburb;
            existingClient.City = client.City;
            existingClient.Postcode = client.Postcode;
            existingClient.Country = client.Country;
            existingClient.VendorId = isAdmin ? client.VendorId : vendorId ?? existingClient.VendorId;

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ClientExists(client.Id))
            {
                return NotFound();
            }

            throw;
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Unable to save client right now. Please check the form values and try again.");
            if (!isAdmin && vendorId.HasValue)
            {
                client.VendorId = vendorId.Value;
            }

            return CreateViewWithVendors(client);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();
        if (!isAdmin && vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || !vendorId.HasValue || m.VendorId == vendorId.Value));

        if (client == null)
        {
            return NotFound();
        }

        return View(client);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();
        if (!isAdmin && vendorId == null && IsAuthenticatedUser())
        {
            return Forbid();
        }

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && (isAdmin || !vendorId.HasValue || c.VendorId == vendorId.Value));
        if (client != null)
        {
            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ClientExists(int id)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();
        return await _context.Clients.AnyAsync(e => e.Id == id && (isAdmin || !vendorId.HasValue || e.VendorId == vendorId.Value));
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

    private bool IsAdminUser()
    {
        return ControllerContext?.HttpContext?.User?.FindFirst("UserType")?.Value == ((int)UserType.Admin).ToString();
    }

    private ViewResult CreateViewWithVendors(Client client)
    {
        var allowVendorSelection = IsAdminUser();
        ViewBag.AllowVendorSelection = allowVendorSelection;
        ViewBag.IsVendorSelectionLocked = false;

        if (allowVendorSelection)
        {
            var vendorOptions = _context.Vendors
                .AsNoTracking()
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem
                {
                    Value = v.Id.ToString(),
                    Text = v.Name
                })
                .ToList();

            if (vendorOptions.Count == 1)
            {
                if (client.VendorId <= 0)
                {
                    client.VendorId = int.Parse(vendorOptions[0].Value!);
                }

                ViewBag.IsVendorSelectionLocked = true;
            }

            ViewBag.VendorOptions = vendorOptions;
        }

        return View(client);
    }

    private static string IncrementCardId(string? lastCardId)
    {
        if (!int.TryParse(lastCardId?.Trim(), out var number) || number < 0)
        {
            number = 0;
        }

        return (number + 1).ToString();
    }

    private static string EscapeTsv(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\t", " ")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }
}
