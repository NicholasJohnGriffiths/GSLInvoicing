using GSLInvoicing.Web.Data;
using GSLInvoicing.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GSLInvoicing.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AppUsersController : Controller
{
    private readonly AppDbContext _context;

    public AppUsersController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var appUsers = await _context.AppUsers
            .AsNoTracking()
            .Include(u => u.Vendor)
            .OrderBy(u => u.Vendor.Name)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        return View(appUsers);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateSelections();
        return View(new AppUser { UserType = UserType.General });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("UserName,Password,Email,Phone,VendorId,UserType")] AppUser appUser)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSelections(appUser.VendorId, appUser.UserType);
            return View(appUser);
        }

        _context.AppUsers.Add(appUser);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var appUser = await _context.AppUsers.FindAsync(id);
        if (appUser == null)
        {
            return NotFound();
        }

        await PopulateSelections(appUser.VendorId, appUser.UserType);
        return View(appUser);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,UserName,Password,Email,Phone,VendorId,UserType")] AppUser appUser)
    {
        if (id != appUser.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateSelections(appUser.VendorId, appUser.UserType);
            return View(appUser);
        }

        _context.Update(appUser);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var appUser = await _context.AppUsers
            .AsNoTracking()
            .Include(u => u.Vendor)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (appUser == null)
        {
            return NotFound();
        }

        return View(appUser);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var appUser = await _context.AppUsers.FindAsync(id);
        if (appUser != null)
        {
            _context.AppUsers.Remove(appUser);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSelections(int? selectedVendorId = null, UserType selectedUserType = UserType.General)
    {
        var vendors = await _context.Vendors
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync();

        ViewBag.Vendors = new SelectList(vendors, nameof(Vendor.Id), nameof(Vendor.Name), selectedVendorId);
        ViewBag.UserTypes = new SelectList(
            Enum.GetValues<UserType>().Select(t => new { Value = (int)t, Text = t.ToString() }),
            "Value",
            "Text",
            (int)selectedUserType);
    }
}