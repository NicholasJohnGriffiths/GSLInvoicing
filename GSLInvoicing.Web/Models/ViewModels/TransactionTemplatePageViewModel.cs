using Microsoft.AspNetCore.Http;

namespace GSLInvoicing.Web.Models.ViewModels;

public class TransactionTemplatePageViewModel
{
    public TransactionTemplateDefinition Editor { get; set; } = new();

    public string? TemplateFolderPath { get; set; }

    public List<string> ExistingTemplates { get; set; } = [];

    public string? StatusMessage { get; set; }

    public IFormFile? TemplateFile { get; set; }

    public IFormFile? SampleExcelFile { get; set; }

    public List<string> SampleHeaders { get; set; } = [];
}
