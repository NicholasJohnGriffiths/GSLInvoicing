using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSLInvoicing.Web.Controllers;

public class ConfigController : Controller
{
    private readonly AppDbContext _context;

    public ConfigController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var config = await GetOrCreateSingleConfigAsync();
        return View(config);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var config = await GetOrCreateSingleConfigAsync();

        if (id != config.Id)
        {
            return RedirectToAction(nameof(Edit), new { id = config.Id });
        }

        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,LastInvoiceNumber")] Config config)
    {
        var currentConfig = await GetOrCreateSingleConfigAsync();

        if (id != currentConfig.Id)
        {
            return RedirectToAction(nameof(Edit), new { id = currentConfig.Id });
        }

        if (!ModelState.IsValid)
        {
            return View(config);
        }

        currentConfig.LastInvoiceNumber = config.LastInvoiceNumber;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = currentConfig.Id });
    }

    private async Task<Config> GetOrCreateSingleConfigAsync()
    {
        var config = await _context.Configs
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();

        if (config == null)
        {
            config = new Config
            {
                LastInvoiceNumber = "GSL0000"
            };

            _context.Configs.Add(config);
            await _context.SaveChangesAsync();
        }

        return config;
    }
}
