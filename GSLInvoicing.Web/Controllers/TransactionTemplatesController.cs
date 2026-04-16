using GSLInvoicing.Web.Models.ViewModels;
using GSLInvoicing.Web.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace GSLInvoicing.Web.Controllers;

public class TransactionTemplatesController : Controller
{
    private readonly ITransactionTemplateService _templateService;

    public TransactionTemplatesController(ITransactionTemplateService templateService)
    {
        _templateService = templateService;
    }

    public async Task<IActionResult> Index(string? templateName = null, string? folderPath = null)
    {
        var model = new TransactionTemplatePageViewModel
        {
            TemplateFolderPath = folderPath,
            ExistingTemplates = (await _templateService.GetTemplateNamesAsync(folderPath)).ToList(),
            StatusMessage = TempData["StatusMessage"] as string
        };

        if (!string.IsNullOrWhiteSpace(templateName))
        {
            var template = await _templateService.GetTemplateAsync(templateName, folderPath);
            if (template != null)
            {
                model.Editor = template;
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TransactionTemplatePageViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Editor.Name))
        {
            ModelState.AddModelError("Editor.Name", "Template name is required.");
        }

        if (!ModelState.IsValid)
        {
            model.ExistingTemplates = (await _templateService.GetTemplateNamesAsync(model.TemplateFolderPath)).ToList();
            return View("Index", model);
        }

        await _templateService.SaveTemplateAsync(model.Editor, model.TemplateFolderPath);
        TempData["StatusMessage"] = $"Template '{model.Editor.Name}' saved.";
        return RedirectToAction(nameof(Index), new { templateName = model.Editor.Name, folderPath = model.TemplateFolderPath });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(TransactionTemplatePageViewModel model)
    {
        if (model.TemplateFile == null || model.TemplateFile.Length == 0)
        {
            TempData["StatusMessage"] = "Select a template file to import.";
            return RedirectToAction(nameof(Index), new { folderPath = model.TemplateFolderPath });
        }

        await using var stream = model.TemplateFile.OpenReadStream();
        await _templateService.ImportTemplateAsync(stream, model.TemplateFile.FileName, model.TemplateFolderPath);

        TempData["StatusMessage"] = $"Template '{model.TemplateFile.FileName}' imported.";
        return RedirectToAction(nameof(Index), new { folderPath = model.TemplateFolderPath });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadHeaders(TransactionTemplatePageViewModel model)
    {
        if (model.SampleExcelFile == null || model.SampleExcelFile.Length == 0)
        {
            ModelState.AddModelError(nameof(model.SampleExcelFile), "Select an Excel file first.");
            model.ExistingTemplates = (await _templateService.GetTemplateNamesAsync(model.TemplateFolderPath)).ToList();
            return View("Index", model);
        }

        await using var stream = model.SampleExcelFile.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var usedRange = worksheet.RangeUsed();

        model.SampleHeaders = [];
        if (usedRange != null)
        {
            foreach (var cell in usedRange.FirstRow().CellsUsed())
            {
                var header = cell.GetString().Trim();
                if (!string.IsNullOrWhiteSpace(header))
                {
                    model.SampleHeaders.Add(header);
                }
            }
        }

        model.ExistingTemplates = (await _templateService.GetTemplateNamesAsync(model.TemplateFolderPath)).ToList();
        model.StatusMessage = model.SampleHeaders.Count == 0
            ? "No headers found in first row of Excel file."
            : $"Loaded {model.SampleHeaders.Count} header(s) from Excel.";

        return View("Index", model);
    }
}
