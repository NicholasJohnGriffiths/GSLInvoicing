using System.Text.Json;
using GSLInvoicing.Web.Models.ViewModels;

namespace GSLInvoicing.Web.Services;

public interface ITransactionTemplateService
{
    Task<IReadOnlyList<string>> GetTemplateNamesAsync(string? folderPath = null);

    Task<TransactionTemplateDefinition?> GetTemplateAsync(string templateName, string? folderPath = null);

    Task SaveTemplateAsync(TransactionTemplateDefinition template, string? folderPath = null);

    Task ImportTemplateAsync(Stream fileStream, string fileName, string? folderPath = null);
}

public class TransactionTemplateService : ITransactionTemplateService
{
    private readonly IWebHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public TransactionTemplateService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<IReadOnlyList<string>> GetTemplateNamesAsync(string? folderPath = null)
    {
        var dir = EnsureDirectory(folderPath);
        var names = Directory
            .EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public async Task<TransactionTemplateDefinition?> GetTemplateAsync(string templateName, string? folderPath = null)
    {
        var path = GetTemplatePath(templateName, folderPath);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<TransactionTemplateDefinition>(stream);
    }

    public async Task SaveTemplateAsync(TransactionTemplateDefinition template, string? folderPath = null)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new InvalidOperationException("Template name is required.");
        }

        var safeName = MakeSafeFileName(template.Name);
        template.Name = safeName;
        var path = GetTemplatePath(safeName, folderPath);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, template, JsonOptions);
    }

    public async Task ImportTemplateAsync(Stream fileStream, string fileName, string? folderPath = null)
    {
        if (fileStream.Length == 0)
        {
            throw new InvalidOperationException("Template file is empty.");
        }

        TransactionTemplateDefinition? template;
        using (var memory = new MemoryStream())
        {
            await fileStream.CopyToAsync(memory);
            memory.Position = 0;
            template = await JsonSerializer.DeserializeAsync<TransactionTemplateDefinition>(memory);
        }

        if (template == null)
        {
            throw new InvalidOperationException("Template file is invalid JSON.");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            template.Name = Path.GetFileNameWithoutExtension(fileName);
        }

        await SaveTemplateAsync(template, folderPath);
    }

    private string EnsureDirectory(string? folderPath)
    {
        var dir = ResolveDirectory(folderPath);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string GetTemplatePath(string templateName, string? folderPath)
    {
        var safe = MakeSafeFileName(templateName);
        return Path.Combine(EnsureDirectory(folderPath), $"{safe}.json");
    }

    private string ResolveDirectory(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return Path.Combine(_environment.ContentRootPath, "TransactionTemplates");
        }

        var trimmed = folderPath.Trim();
        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, trimmed));
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim().Where(ch => !invalid.Contains(ch)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "template" : cleaned;
    }
}
