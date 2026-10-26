using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWMS.Data;
using SmartWMS.Models;
using SmartWMS.ViewModels;

namespace SmartWMS.Controllers;

public class MasterController : Controller
{
    private readonly AppDbContext _db;

    public MasterController(AppDbContext db)
    {
        _db = db;
    }

    // 1. MASTER ITEMS / SKU
    [HttpGet]
    public async Task<IActionResult> Items()
    {
        var vm = new MasterItemsVm
        {
            Items = await _db.Items.Include(i => i.Category).Include(i => i.Balances).OrderBy(i => i.PartNumber).ToListAsync(),
            Categories = await _db.ItemCategories.OrderBy(c => c.Name).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(Item item)
    {
        if (ModelState.IsValid)
        {
            item.PartNumber = item.PartNumber.Trim().ToUpper();
            if (await _db.Items.AnyAsync(i => i.PartNumber == item.PartNumber))
            {
                TempData["Error"] = $"Part Number '{item.PartNumber}' sudah terdaftar!";
                return RedirectToAction(nameof(Items));
            }

            _db.Items.Add(item);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Master Item '{item.PartNumber}' berhasil ditambahkan.";
            return RedirectToAction(nameof(Items));
        }

        TempData["Error"] = "Formulir tidak valid. Pastikan semua field terisi benar.";
        return RedirectToAction(nameof(Items));
    }

    // 2. MASTER STORAGE LOCATIONS & RACKS
    [HttpGet]
    public async Task<IActionResult> Locations()
    {
        var vm = new MasterLocationsVm
        {
            Locations = await _db.StorageLocations.Include(l => l.Warehouse).OrderBy(l => l.LocationCode).ToListAsync(),
            Warehouses = await _db.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLocation(StorageLocation loc)
    {
        if (ModelState.IsValid)
        {
            loc.LocationCode = loc.LocationCode.Trim().ToUpper();
            if (await _db.StorageLocations.AnyAsync(l => l.LocationCode == loc.LocationCode))
            {
                TempData["Error"] = $"Kode Lokasi '{loc.LocationCode}' sudah digunakan!";
                return RedirectToAction(nameof(Locations));
            }

            if (loc.MaxCapacity <= 0) loc.MaxCapacity = 100;
            _db.StorageLocations.Add(loc);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Lokasi '{loc.LocationCode}' berhasil didaftarkan.";
            return RedirectToAction(nameof(Locations));
        }

        TempData["Error"] = "Gagal menambahkan lokasi. Periksa input Anda.";
        return RedirectToAction(nameof(Locations));
    }

    // 3. ENTERPRISE STOCK MOVEMENT LEDGER (AUDIT TRAIL)
    [HttpGet]
    public async Task<IActionResult> AuditTrail(string? type, string? part)
    {
        var query = _db.StockMovements
            .Include(m => m.Item)
            .Include(m => m.FromLocation)
            .Include(m => m.ToLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(m => m.MovementType == type);

        if (!string.IsNullOrWhiteSpace(part))
            query = query.Where(m => m.Item.PartNumber == part);

        var vm = new AuditTrailVm
        {
            SelectedType = type,
            SelectedPart = part,
            Movements = await query.OrderByDescending(m => m.CreatedAt).Take(100).ToListAsync(),
            Items = await _db.Items.OrderBy(i => i.PartNumber).ToListAsync()
        };

        return View(vm);
    }

    // 4. GENERATOR & CETAK LABEL BARCODE / QR CODE (RAK & SKU)
    [HttpGet]
    public async Task<IActionResult> PrintLabels(string? type)
    {
        ViewBag.Type = type ?? "RACK";
        ViewBag.Locations = await _db.StorageLocations.Where(l => l.IsActive).OrderBy(l => l.LocationCode).ToListAsync();
        ViewBag.Items = await _db.Items.OrderBy(i => i.PartNumber).ToListAsync();
        return View();
    }
}
