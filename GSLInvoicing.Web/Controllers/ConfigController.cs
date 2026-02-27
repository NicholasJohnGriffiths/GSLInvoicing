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
        var rows = await _context.Configs
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync();

        return View(rows);
    }

    public IActionResult Create()
    {
        return View(new Config());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,LastInvoiceNumber")] Config config)
    {
        if (!ModelState.IsValid)
        {
            return View(config);
        }

        _context.Configs.Add(config);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var config = await _context.Configs.FindAsync(id);
        if (config == null)
        {
            return NotFound();
        }

        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,LastInvoiceNumber")] Config config)
    {
        if (id != config.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(config);
        }

        _context.Update(config);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var config = await _context.Configs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (config == null)
        {
            return NotFound();
        }

        return View(config);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var config = await _context.Configs.FindAsync(id);
        if (config != null)
        {
            _context.Configs.Remove(config);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
