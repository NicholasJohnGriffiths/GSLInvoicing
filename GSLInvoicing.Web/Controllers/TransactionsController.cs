using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using GSLInvoicing.Web.Models.ViewModels;
using GSLInvoicing.Web.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace GSLInvoicing.Web.Controllers;

public class TransactionsController : Controller
{
    private static readonly string[] AllowedSortFields =
    [
        nameof(Transaction.TransDate),
        nameof(Transaction.TransType),
        nameof(Transaction.Title),
        nameof(Transaction.Detail),
        nameof(Transaction.Particulars),
        nameof(Transaction.Code),
        nameof(Transaction.Reference),
        nameof(Transaction.Amount),
        nameof(Transaction.AccountNumber),
        "Client"
    ];

    private readonly AppDbContext _context;
    private readonly ITransactionTemplateService _templateService;

    public TransactionsController(AppDbContext context, ITransactionTemplateService templateService)
    {
        _context = context;
        _templateService = templateService;
    }

    public async Task<IActionResult> Index(int? clientId = null, DateOnly? startDate = null, DateOnly? endDate = null, string? sortBy = null, string? direction = null, string? templateFolderPath = null)
    {
        if (!CanAccessScopedData())
        {
            return Forbid();
        }

        var statusMessage = TempData["StatusMessage"] as string;
        return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, null, null, statusMessage, null, templateFolderPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewImport(int? clientId, DateOnly? startDate, DateOnly? endDate, string? sortBy, string? direction, string? templateName, string? templateFolderPath, IFormFile? excelFile)
    {
        if (!CanAccessScopedData())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(templateName))
        {
            return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, null, null, "Select a transaction template.", null, templateFolderPath);
        }

        if (excelFile == null || excelFile.Length == 0)
        {
            return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, null, templateName, "Select an Excel file to import.", null, templateFolderPath);
        }

        var template = await _templateService.GetTemplateAsync(templateName, templateFolderPath);
        if (template == null)
        {
            return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, null, null, "Selected template was not found.", null, templateFolderPath);
        }

        await using var stream = excelFile.OpenReadStream();
        var preview = await BuildImportPreviewAsync(stream, template);

        var payload = JsonSerializer.Serialize(preview);
        return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, preview, template.Name, null, payload, null, templateFolderPath);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(
        int? clientId,
        DateOnly? startDate,
        DateOnly? endDate,
        string? sortBy,
        string? direction,
        string? previewPayload,
        string? templateFolderPath,
        List<int>? previewRowNumbers,
        List<string?>? selectedClientIds)
    {
        if (!CanAccessScopedData())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(previewPayload))
        {
            return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, null, null, "No preview data to import.", null, templateFolderPath);
        }

        var preview = JsonSerializer.Deserialize<TransactionImportPreview>(previewPayload);
        if (preview == null)
        {
            return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, null, null, "Preview data could not be read.", null, templateFolderPath);
        }

        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();
        var allowedClients = _context.Clients.AsNoTracking();
        if (!isAdmin && vendorId.HasValue)
        {
            allowedClients = allowedClients.Where(c => c.VendorId == vendorId.Value);
        }

        var allowedClientLookup = await allowedClients
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        var allowedClientIds = allowedClientLookup.Select(c => c.Id).ToHashSet();
        var allowedClientNames = allowedClientLookup.ToDictionary(c => c.Id, c => c.Name);

        var postedClientSelectionByRow = new Dictionary<int, int?>();
        if (previewRowNumbers != null && selectedClientIds != null)
        {
            for (var i = 0; i < previewRowNumbers.Count; i++)
            {
                var selectedValue = i < selectedClientIds.Count ? selectedClientIds[i] : null;
                if (int.TryParse(selectedValue, out var selectedId) && selectedId > 0)
                {
                    postedClientSelectionByRow[previewRowNumbers[i]] = selectedId;
                }
                else
                {
                    postedClientSelectionByRow[previewRowNumbers[i]] = null;
                }
            }
        }

        foreach (var row in preview.Rows)
        {
            if (postedClientSelectionByRow.TryGetValue(row.RowNumber, out var selectedClientId) && selectedClientId.HasValue)
            {
                if (allowedClientIds.Contains(selectedClientId.Value))
                {
                    row.MatchedClientId = selectedClientId;
                    row.MatchedClientName = allowedClientNames.GetValueOrDefault(selectedClientId.Value);
                    row.MatchReason = "Selected manually in preview";
                    row.Error = RemoveNoMatchingClientError(row.Error);
                }
                else
                {
                    row.MatchedClientId = null;
                }
            }
            else if (row.MatchedClientId.HasValue && !allowedClientIds.Contains(row.MatchedClientId.Value))
            {
                row.MatchedClientId = null;
            }
        }

        var toImport = preview.Rows
            .Where(r => string.IsNullOrWhiteSpace(r.Error)
                        && r.MatchedClientId.HasValue
                        && r.TransDate.HasValue
                        && r.Amount.HasValue)
            .ToList();

        if (toImport.Count == 0)
        {
            return await BuildIndexView(clientId, startDate, endDate, sortBy, direction, preview, preview.TemplateName, "No valid rows were found to import.", previewPayload, null, templateFolderPath);
        }

        foreach (var row in toImport)
        {
            _context.Transactions.Add(new Transaction
            {
                ClientId = row.MatchedClientId!.Value,
                TransDate = row.TransDate!.Value,
                TransType = row.TransType is "Credit" or "Debit" ? row.TransType : "Debit",
                Title = row.Title,
                Detail = row.Detail,
                Particulars = row.Particulars,
                Code = row.Code,
                Reference = row.Reference,
                Amount = row.Amount!.Value,
                AccountNumber = row.AccountNumber
            });
        }

        await _context.SaveChangesAsync();
        TempData["StatusMessage"] = $"Imported {toImport.Count} transaction row(s).";
        return RedirectToAction(nameof(Index), new { clientId, startDate, endDate, sortBy, direction, templateFolderPath });
    }

    private async Task<ViewResult> BuildIndexView(
        int? clientId,
        DateOnly? startDate,
        DateOnly? endDate,
        string? sortBy,
        string? direction,
        TransactionImportPreview? preview,
        string? selectedTemplateName,
        string? importMessage,
        string? previewPayload = null,
        string? statusMessage = null,
        string? templateFolderPath = null)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();
        var (normalizedStartDate, normalizedEndDate) = NormalizeDateRange(startDate, endDate);

        var normalizedSortBy = NormalizeSortBy(sortBy);
        var normalizedDirection = NormalizeDirection(direction);

        var allowedClients = _context.Clients.AsNoTracking();
        if (!isAdmin && vendorId.HasValue)
        {
            allowedClients = allowedClients.Where(c => c.VendorId == vendorId.Value);
        }

        var clientOptions = await allowedClients
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();

        IQueryable<Transaction> query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Client);

        if (!isAdmin && vendorId.HasValue)
        {
            query = query.Where(t => t.Client.VendorId == vendorId.Value);
        }

        if (clientId.HasValue && clientId.Value > 0)
        {
            query = query.Where(t => t.ClientId == clientId.Value);
        }

        var startDateTime = normalizedStartDate.ToDateTime(TimeOnly.MinValue);
        var endExclusive = normalizedEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        query = query.Where(t => t.TransDate >= startDateTime && t.TransDate < endExclusive);

        query = ApplySorting(query, normalizedSortBy, normalizedDirection);

        var items = await query.ToListAsync();

        ViewBag.ClientOptions = clientOptions;
        ViewBag.SelectedClientId = clientId;
        ViewBag.StartDate = normalizedStartDate;
        ViewBag.EndDate = normalizedEndDate;
        ViewBag.SortBy = normalizedSortBy;
        ViewBag.Direction = normalizedDirection;
        ViewBag.TemplateFolderPath = templateFolderPath;
        ViewBag.TemplateOptions = (await _templateService.GetTemplateNamesAsync(templateFolderPath))
            .Select(name => new SelectListItem
            {
                Value = name,
                Text = name,
                Selected = string.Equals(name, selectedTemplateName, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
        ViewBag.SelectedTemplateName = selectedTemplateName;
        ViewBag.ImportPreview = preview;
        ViewBag.PreviewPayload = previewPayload;
        ViewBag.ImportMessage = importMessage;
        ViewBag.StatusMessage = statusMessage;

        return View("Index", items);
    }

    private static (DateOnly StartDate, DateOnly EndDate) NormalizeDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var normalizedStartDate = startDate ?? new DateOnly(today.Year, today.Month, 1);
        var normalizedEndDate = endDate ?? new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        if (normalizedEndDate < normalizedStartDate)
        {
            (normalizedStartDate, normalizedEndDate) = (normalizedEndDate, normalizedStartDate);
        }

        return (normalizedStartDate, normalizedEndDate);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var transaction = await GetTransactionByScope(id.Value);
        if (transaction == null)
        {
            return NotFound();
        }

        return View(transaction);
    }

    public async Task<IActionResult> Create()
    {
        var transaction = new Transaction
        {
            TransDate = DateTime.Now,
            TransType = "Debit"
        };

        return await CreateOrEditView(transaction);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ClientId,TransDate,TransType,Title,Detail,Particulars,Code,Reference,Amount,AccountNumber")] Transaction transaction)
    {
        await EnforceValidClientSelection(transaction);

        if (!ModelState.IsValid)
        {
            return await CreateOrEditView(transaction);
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { clientId = transaction.ClientId });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var transaction = await GetTransactionByScope(id.Value);
        if (transaction == null)
        {
            return NotFound();
        }

        return await CreateOrEditView(transaction);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,ClientId,TransDate,TransType,Title,Detail,Particulars,Code,Reference,Amount,AccountNumber")] Transaction transaction)
    {
        if (id != transaction.Id)
        {
            return NotFound();
        }

        await EnforceValidClientSelection(transaction);

        if (!ModelState.IsValid)
        {
            return await CreateOrEditView(transaction);
        }

        var existing = await GetTransactionByScope(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.ClientId = transaction.ClientId;
        existing.TransDate = transaction.TransDate;
        existing.TransType = transaction.TransType;
        existing.Title = transaction.Title;
        existing.Detail = transaction.Detail;
        existing.Particulars = transaction.Particulars;
        existing.Code = transaction.Code;
        existing.Reference = transaction.Reference;
        existing.Amount = transaction.Amount;
        existing.AccountNumber = transaction.AccountNumber;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { clientId = existing.ClientId });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var transaction = await GetTransactionByScope(id.Value);
        if (transaction == null)
        {
            return NotFound();
        }

        return View(transaction);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var transaction = await GetTransactionByScope(id);
        if (transaction == null)
        {
            return NotFound();
        }

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<Transaction?> GetTransactionByScope(int id)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();

        if (!isAdmin && vendorId == null && IsAuthenticatedUser())
        {
            return null;
        }

        return await _context.Transactions
            .Include(t => t.Client)
            .FirstOrDefaultAsync(t => t.Id == id && (isAdmin || !vendorId.HasValue || t.Client.VendorId == vendorId.Value));
    }

    private async Task EnforceValidClientSelection(Transaction transaction)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();

        if (!isAdmin && vendorId == null && IsAuthenticatedUser())
        {
            ModelState.AddModelError(nameof(Transaction.ClientId), "No vendor is linked to your account.");
            return;
        }

        var allowedClients = _context.Clients.AsNoTracking();
        if (!isAdmin && vendorId.HasValue)
        {
            allowedClients = allowedClients.Where(c => c.VendorId == vendorId.Value);
        }

        var exists = await allowedClients.AnyAsync(c => c.Id == transaction.ClientId);
        if (!exists)
        {
            ModelState.AddModelError(nameof(Transaction.ClientId), "Please select a valid client.");
        }

        if (transaction.TransType is not "Credit" and not "Debit")
        {
            ModelState.AddModelError(nameof(Transaction.TransType), "Transaction type must be Credit or Debit.");
        }
    }

    private async Task<TransactionImportPreview> BuildImportPreviewAsync(Stream excelStream, TransactionTemplateDefinition template)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();

        var scopedClientsQuery = _context.Clients.AsNoTracking();
        if (!isAdmin && vendorId.HasValue)
        {
            scopedClientsQuery = scopedClientsQuery.Where(c => c.VendorId == vendorId.Value);
        }

        var scopedClients = await scopedClientsQuery
            .Select(c => new ClientMatchCandidate(c.Id, c.Name, c.Contact, c.TransactionReference))
            .ToListAsync();

        var scopedInvoiceQuery = _context.Invoices.AsNoTracking().Include(i => i.Client).AsQueryable();
        if (!isAdmin && vendorId.HasValue)
        {
            scopedInvoiceQuery = scopedInvoiceQuery.Where(i => i.Client.VendorId == vendorId.Value);
        }

        var invoices = await scopedInvoiceQuery
            .Select(i => new InvoiceMatchCandidate(i.InvoiceNumber, i.ClientId))
            .ToListAsync();

        using var workbook = new XLWorkbook(excelStream);
        var worksheet = workbook.Worksheets.First();
        var usedRange = worksheet.RangeUsed();

        var result = new TransactionImportPreview
        {
            TemplateName = template.Name
        };

        if (usedRange == null)
        {
            return result;
        }

        var headerRow = usedRange.FirstRow();
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(header) && !headerMap.ContainsKey(header))
            {
                headerMap[header] = cell.Address.ColumnNumber;
            }
        }

        var lastRow = usedRange.LastRow().RowNumber();
        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.CellsUsed().Count() == 0)
            {
                continue;
            }

            var previewRow = new TransactionImportPreviewRow { RowNumber = rowNumber };

            previewRow.TransDate = GetCellDateTime(row, headerMap, template.TransDateColumn);
            previewRow.Detail = GetCellString(row, headerMap, template.DetailColumn);
            previewRow.Particulars = GetCellString(row, headerMap, template.ParticularsColumn);
            previewRow.Code = GetCellString(row, headerMap, template.CodeColumn);
            previewRow.Reference = GetCellString(row, headerMap, template.ReferenceColumn);
            previewRow.Amount = GetCellDecimal(row, headerMap, template.AmountColumn);
            previewRow.AccountNumber = GetCellString(row, headerMap, template.AccountNumberColumn);
            previewRow.TransType = ResolveTransType(GetCellString(row, headerMap, template.TransTypeColumn), previewRow.Amount);

            var matched = MatchClient(
                scopedClients,
                invoices,
                previewRow.Detail,
                previewRow.Particulars,
                previewRow.Code,
                previewRow.Reference,
                previewRow.AccountNumber);
            previewRow.MatchedClientId = matched.clientId;
            previewRow.MatchedClientName = matched.clientName;
            previewRow.MatchReason = matched.reason;

            var errors = new List<string>();
            if (!previewRow.MatchedClientId.HasValue)
            {
                errors.Add("No matching client.");
            }

            if (!previewRow.TransDate.HasValue)
            {
                errors.Add("TransDate missing/invalid.");
            }

            if (previewRow.TransType is not "Credit" and not "Debit")
            {
                errors.Add("TransType must be Credit or Debit.");
            }

            if (!previewRow.Amount.HasValue)
            {
                errors.Add("Amount missing/invalid.");
            }

            previewRow.Error = errors.Count == 0 ? null : string.Join(" ", errors);
            result.Rows.Add(previewRow);
        }

        return result;
    }

    private static (int? clientId, string? clientName, string? reason) MatchClient(
        List<ClientMatchCandidate> clients,
        List<InvoiceMatchCandidate> invoices,
        string? detail,
        string? particulars,
        string? code,
        string? reference,
        string? accountNumber)
    {
        var transactionFields = new[] { detail, particulars, code, reference };

        var byTransactionReferenceToken = FindClientByTransactionReferenceToken(clients, transactionFields);
        if (byTransactionReferenceToken.clientId.HasValue)
        {
            return (byTransactionReferenceToken.clientId, byTransactionReferenceToken.clientName,
                "Matched by Client.TransactionReference token in Detail/Particulars/Code/Reference");
        }

        var invoiceCandidateFields = new[] { detail, particulars, code, reference };
        var byInvoiceInTransactionFields = FindClientByInvoiceNumber(clients, invoices, invoiceCandidateFields);
        if (byInvoiceInTransactionFields.clientId.HasValue)
        {
            return (byInvoiceInTransactionFields.clientId, byInvoiceInTransactionFields.clientName,
                "Matched by Invoice.InvoiceNumber in Detail/Particulars/Code/Reference");
        }

        var searchableParts = new[] { detail, particulars, code, reference, accountNumber };
        var searchableText = string.Join(" ", searchableParts
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim()));

        if (!string.IsNullOrWhiteSpace(searchableText))
        {
            var byInvoice = invoices.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.InvoiceNumber)
                && ContainsIgnoreCase(searchableText, i.InvoiceNumber!));

            if (byInvoice != null)
            {
                var client = clients.FirstOrDefault(c => c.Id == byInvoice.ClientId);
                if (client != null)
                {
                    return (client.Id, client.Name, "Matched by Invoice.InvoiceNumber");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(searchableText))
        {
            var byNameAndContact = clients.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.Name)
                && !string.IsNullOrWhiteSpace(c.Contact)
                && ContainsIgnoreCase(searchableText, c.Name!)
                && ContainsIgnoreCase(searchableText, c.Contact!));

            if (byNameAndContact != null)
            {
                return (byNameAndContact.Id, byNameAndContact.Name, "Matched by Client Name + Contact");
            }
        }

        if (!string.IsNullOrWhiteSpace(searchableText))
        {
            var byName = clients.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Name)
                && ContainsIgnoreCase(searchableText, c.Name!));

            if (byName != null)
            {
                return (byName.Id, byName.Name, "Matched by Client Name");
            }
        }

        return (null, null, null);
    }

    private static (int? clientId, string? clientName) FindClientByInvoiceNumber(
        List<ClientMatchCandidate> clients,
        List<InvoiceMatchCandidate> invoices,
        IEnumerable<string?> fields)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            var value = field.Trim();
            var byInvoice = invoices.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.InvoiceNumber)
                && ContainsIgnoreCase(value, i.InvoiceNumber!));

            if (byInvoice == null)
            {
                continue;
            }

            var client = clients.FirstOrDefault(c => c.Id == byInvoice.ClientId);
            if (client != null)
            {
                return (client.Id, client.Name);
            }
        }

        return (null, null);
    }

    private static (int? clientId, string? clientName) FindClientByTransactionReferenceToken(
        List<ClientMatchCandidate> clients,
        IEnumerable<string?> fields)
    {
        var usableFields = fields
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .ToList();

        if (usableFields.Count == 0)
        {
            return (null, null);
        }

        foreach (var client in clients)
        {
            if (string.IsNullOrWhiteSpace(client.TransactionReference))
            {
                continue;
            }

            var tokens = client.TransactionReference
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Any(token => usableFields.Any(field => ContainsIgnoreCase(field, token))))
            {
                return (client.Id, client.Name);
            }
        }

        return (null, null);
    }

    private static string? GetCellString(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string? headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName) || !headerMap.TryGetValue(headerName.Trim(), out var column))
        {
            return null;
        }

        var value = row.Cell(column).GetString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime? GetCellDateTime(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string? headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName) || !headerMap.TryGetValue(headerName.Trim(), out var column))
        {
            return null;
        }

        var cell = row.Cell(column);
        if (cell.TryGetValue<DateTime>(out var dt))
        {
            return dt;
        }

        var asString = cell.GetString().Trim();
        if (DateTime.TryParse(asString, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static decimal? GetCellDecimal(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string? headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName) || !headerMap.TryGetValue(headerName.Trim(), out var column))
        {
            return null;
        }

        var cell = row.Cell(column);
        if (cell.TryGetValue<decimal>(out var amount))
        {
            return amount;
        }

        var asString = cell.GetString().Trim();
        if (decimal.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out amount)
            || decimal.TryParse(asString, NumberStyles.Any, CultureInfo.CurrentCulture, out amount))
        {
            return amount;
        }

        return null;
    }

    private static string ResolveTransType(string? fromFile, decimal? amount)
    {
        if (amount.HasValue)
        {
            // Amount-driven trans type: negative (payment) -> Credit, positive/zero (money received) -> Debit.
            return amount.Value < 0 ? "Credit" : "Debit";
        }

        var candidate = fromFile?.Trim();
        if (string.Equals(candidate, "credit", StringComparison.OrdinalIgnoreCase))
        {
            return "Credit";
        }

        if (string.Equals(candidate, "debit", StringComparison.OrdinalIgnoreCase))
        {
            return "Debit";
        }

        return candidate ?? string.Empty;
    }

    private static bool ContainsIgnoreCase(string haystack, string needle)
    {
        return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string? RemoveNoMatchingClientError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        var cleaned = error.Replace("No matching client.", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private async Task<ViewResult> CreateOrEditView(Transaction transaction)
    {
        var isAdmin = IsAdminUser();
        var vendorId = GetCurrentVendorId();

        var allowedClients = _context.Clients.AsNoTracking();
        if (!isAdmin && vendorId.HasValue)
        {
            allowedClients = allowedClients.Where(c => c.VendorId == vendorId.Value);
        }

        ViewBag.ClientOptions = await allowedClients
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();

        ViewBag.TransTypeOptions = new List<SelectListItem>
        {
            new() { Value = "Credit", Text = "Credit" },
            new() { Value = "Debit", Text = "Debit" }
        };

        return View(transaction);
    }

    private static IQueryable<Transaction> ApplySorting(IQueryable<Transaction> query, string sortBy, string direction)
    {
        var ascending = direction == "asc";

        return (sortBy, ascending) switch
        {
            ("Client", true) => query.OrderBy(t => t.Client.Name).ThenBy(t => t.Id),
            ("Client", false) => query.OrderByDescending(t => t.Client.Name).ThenByDescending(t => t.Id),
            (nameof(Transaction.TransType), true) => query.OrderBy(t => t.TransType).ThenBy(t => t.Id),
            (nameof(Transaction.TransType), false) => query.OrderByDescending(t => t.TransType).ThenByDescending(t => t.Id),
            (nameof(Transaction.Title), true) => query.OrderBy(t => t.Title).ThenBy(t => t.Id),
            (nameof(Transaction.Title), false) => query.OrderByDescending(t => t.Title).ThenByDescending(t => t.Id),
            (nameof(Transaction.Detail), true) => query.OrderBy(t => t.Detail).ThenBy(t => t.Id),
            (nameof(Transaction.Detail), false) => query.OrderByDescending(t => t.Detail).ThenByDescending(t => t.Id),
            (nameof(Transaction.Particulars), true) => query.OrderBy(t => t.Particulars).ThenBy(t => t.Id),
            (nameof(Transaction.Particulars), false) => query.OrderByDescending(t => t.Particulars).ThenByDescending(t => t.Id),
            (nameof(Transaction.Code), true) => query.OrderBy(t => t.Code).ThenBy(t => t.Id),
            (nameof(Transaction.Code), false) => query.OrderByDescending(t => t.Code).ThenByDescending(t => t.Id),
            (nameof(Transaction.Reference), true) => query.OrderBy(t => t.Reference).ThenBy(t => t.Id),
            (nameof(Transaction.Reference), false) => query.OrderByDescending(t => t.Reference).ThenByDescending(t => t.Id),
            (nameof(Transaction.Amount), true) => query.OrderBy(t => t.Amount).ThenBy(t => t.Id),
            (nameof(Transaction.Amount), false) => query.OrderByDescending(t => t.Amount).ThenByDescending(t => t.Id),
            (nameof(Transaction.AccountNumber), true) => query.OrderBy(t => t.AccountNumber).ThenBy(t => t.Id),
            (nameof(Transaction.AccountNumber), false) => query.OrderByDescending(t => t.AccountNumber).ThenByDescending(t => t.Id),
            (nameof(Transaction.TransDate), true) => query.OrderBy(t => t.TransDate).ThenBy(t => t.Id),
            _ => query.OrderByDescending(t => t.TransDate).ThenByDescending(t => t.Id)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return nameof(Transaction.TransDate);
        }

        return AllowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase)
            ? AllowedSortFields.First(f => string.Equals(f, sortBy, StringComparison.OrdinalIgnoreCase))
            : nameof(Transaction.TransDate);
    }

    private static string NormalizeDirection(string? direction)
    {
        return string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
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

    private sealed record ClientMatchCandidate(int Id, string? Name, string? Contact, string? TransactionReference);

    private sealed record InvoiceMatchCandidate(string? InvoiceNumber, int ClientId);
}
