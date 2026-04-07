using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSLInvoicing.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class VendorsController : Controller
{
    private readonly AppDbContext _context;

    public VendorsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var vendors = await _context.Vendors
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync();

        return View(vendors);
    }

    public IActionResult Create()
    {
        return View(new Vendor());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Address,BankDetails,Email,Phone,GSTNumber")] Vendor vendor)
    {
        if (!ModelState.IsValid)
        {
            return View(vendor);
        }

        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var vendor = await _context.Vendors.FindAsync(id);
        if (vendor == null)
        {
            return NotFound();
        }

        return View(vendor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,BankDetails,Email,Phone,GSTNumber")] Vendor vendor)
    {
        if (id != vendor.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(vendor);
        }

        _context.Update(vendor);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var vendor = await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vendor == null)
        {
            return NotFound();
        }

        return View(vendor);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var vendor = await _context.Vendors.FindAsync(id);
        if (vendor != null)
        {
            _context.Vendors.Remove(vendor);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}