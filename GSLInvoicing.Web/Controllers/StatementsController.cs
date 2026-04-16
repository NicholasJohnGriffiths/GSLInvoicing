using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using GSLInvoicing.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GSLInvoicing.Web.Controllers;

public class StatementsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public StatementsController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index(DateTime? fromDate = null, DateTime? toDate = null, int? clientId = null, string? outputFolderPath = null)
    {
        if (!CanAccessScopedData())
        {
            return Forbid();
        }

        var statusMessage = TempData["StatusMessage"] as string;
        var model = await BuildPageModelAsync(fromDate, toDate, clientId, outputFolderPath, statusMessage);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Print(StatementPageViewModel model)
    {
        if (!CanAccessScopedData())
        {
            return Forbid();
        }

        if (!model.SelectedClientId.HasValue)
        {
            ModelState.AddModelError(nameof(model.SelectedClientId), "Select a client first.");
        }

        if (string.IsNullOrWhiteSpace(model.OutputFolderPath))
        {
            ModelState.AddModelError(nameof(model.OutputFolderPath), "Select an output folder path.");
        }

        var pageModel = await BuildPageModelAsync(model.FromDate, model.ToDate, model.SelectedClientId, model.OutputFolderPath, null);

        if (!ModelState.IsValid)
        {
            return View("Index", pageModel);
        }

        var outputDir = ResolveOutputDirectory(model.OutputFolderPath!);
        Directory.CreateDirectory(outputDir);

        var fileName = BuildStatementFileName(pageModel);
        var fullPath = Path.Combine(outputDir, fileName);

        var pdfBytes = StatementPdfGenerator.Generate(pageModel);
        await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes);

        TempData["StatusMessage"] = $"Statement PDF saved to: {fullPath}";
        return RedirectToAction(nameof(Index), new
        {
            fromDate = pageModel.FromDate.ToString("yyyy-MM-dd"),
            toDate = pageModel.ToDate.ToString("yyyy-MM-dd"),
            clientId = pageModel.SelectedClientId,
            outputFolderPath = model.OutputFolderPath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Download(StatementPageViewModel model)
    {
        if (!CanAccessScopedData())
        {
            return Forbid();
        }

        if (!model.SelectedClientId.HasValue)
        {
            ModelState.AddModelError(nameof(model.SelectedClientId), "Select a client first.");
        }

        var pageModel = await BuildPageModelAsync(model.FromDate, model.ToDate, model.SelectedClientId, model.OutputFolderPath, null);
        if (!ModelState.IsValid)
        {
            return View("Index", pageModel);
        }

        var pdfBytes = StatementPdfGenerator.Generate(pageModel);
        var fileName = BuildStatementFileName(pageModel);
        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task<StatementPageViewModel> BuildPageModelAsync(
        DateTime? fromDate,
        DateTime? toDate,
        int? clientId,
        string? outputFolderPath,
        string? statusMessage)
    {
        var today = DateTime.Today;
        var defaultFromDate = new DateTime(today.Year, today.Month, 1);
        var defaultToDate = defaultFromDate.AddMonths(1).AddDays(-1);

        var selectedFromDate = (fromDate ?? defaultFromDate).Date;
        var selectedToDate = (toDate ?? defaultToDate).Date;
        if (selectedToDate < selectedFromDate)
        {
            selectedToDate = selectedFromDate;
        }

        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();

        var scopedClients = _context.Clients.AsNoTracking();
        if (!isAdmin && vendorId.HasValue)
        {
            scopedClients = scopedClients.Where(c => c.VendorId == vendorId.Value);
        }

        var clientOptions = await scopedClients
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = clientId.HasValue && c.Id == clientId.Value
            })
            .ToListAsync();

        var model = new StatementPageViewModel
        {
            FromDate = selectedFromDate,
            ToDate = selectedToDate,
            SelectedClientId = clientId,
            OutputFolderPath = outputFolderPath,
            StatusMessage = statusMessage,
            ClientOptions = clientOptions
        };

        if (!clientId.HasValue)
        {
            return model;
        }

        var client = await scopedClients.FirstOrDefaultAsync(c => c.Id == clientId.Value);
        if (client == null)
        {
            model.SelectedClientId = null;
            model.StatusMessage = "Selected client is not available.";
            return model;
        }

        model.ClientName = client.Name;
        model.ClientContact = client.Contact;
        model.ClientEmail = client.Email;

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.ClientId == clientId.Value
                        && t.TransDate.Date >= selectedFromDate
                        && t.TransDate.Date <= selectedToDate)
            .OrderBy(t => t.TransDate)
            .ThenBy(t => t.Id)
            .ToListAsync();

        decimal running = 0;
        foreach (var transaction in transactions)
        {
            running += transaction.Amount;
            model.Transactions.Add(new StatementTransactionRow
            {
                TransDate = transaction.TransDate,
                TransType = transaction.TransType,
                Detail = transaction.Detail,
                Particulars = transaction.Particulars,
                Code = transaction.Code,
                Reference = transaction.Reference,
                Amount = transaction.Amount,
                RunningBalance = running
            });
        }

        model.ClosingBalance = running;
        return model;
    }

    private string ResolveOutputDirectory(string folderPath)
    {
        var trimmed = folderPath.Trim();
        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, trimmed));
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "statement" : cleaned;
    }

    private static string BuildStatementFileName(StatementPageViewModel model)
    {
        var safeClient = MakeSafeFileName(model.ClientName ?? "client");
        return $"Statement-{safeClient}-{model.FromDate:yyyyMMdd}-{model.ToDate:yyyyMMdd}.pdf";
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

    private bool CanAccessScopedData()
    {
        return IsAdminUser() || GetCurrentVendorId().HasValue || !IsAuthenticatedUser();
    }
}
