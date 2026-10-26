using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWMS.Data;
using SmartWMS.Models;
using SmartWMS.Services;
using SmartWMS.ViewModels;

namespace SmartWMS.Controllers;

public class InternalController : Controller
{
    private readonly AppDbContext _db;
    private readonly IInternalService _svc;

    public InternalController(AppDbContext db, IInternalService svc)
    {
        _db = db;
        _svc = svc;
    }

    // 1. RELOKASI / TRANSFER STOK ANTAR RAK (BIN-TO-BIN)
    [HttpGet]
    public async Task<IActionResult> Transfer()
    {
        var vm = new StockTransferVm
        {
            AvailableBalances = await _db.StoringBalances
                .Include(b => b.Item)
                .Include(b => b.Location)
                .Where(b => b.Quantity > 0)
                .OrderBy(b => b.Location.LocationCode)
                .ThenBy(b => b.Item.PartNumber)
                .ToListAsync(),
            TargetLocations = await _db.StorageLocations
                .Where(l => l.IsActive)
                .OrderBy(l => l.LocationCode)
                .ToListAsync(),
            RecentTransfers = await _db.StockMovements
                .Include(m => m.Item)
                .Include(m => m.FromLocation)
                .Include(m => m.ToLocation)
                .Where(m => m.MovementType == MovementType.Transfer)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .ToListAsync()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTransfer(int balanceId, int targetLocationId, int qty, string operatorName, string notes)
    {
        var (ok, msg) = await _svc.TransferStockAsync(balanceId, targetLocationId, qty, operatorName, notes);
        if (ok)
            TempData["Success"] = msg;
        else
            TempData["Error"] = msg;

        return RedirectToAction(nameof(Transfer));
    }

    // 2. STOCK OPNAME & REKONSILIASI FISIK (CYCLE COUNTING)
    [HttpGet]
    public async Task<IActionResult> StockOpname(string? location)
    {
        var query = _db.StoringBalances
            .Include(b => b.Item)
            .Include(b => b.Location)
            .Where(b => b.Quantity > 0)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(b => b.Location.LocationCode == location);

        var vm = new StockOpnameVm
        {
            SelectedLocation = location,
            Locations = await _db.StorageLocations.Where(l => l.IsActive).OrderBy(l => l.LocationCode).ToListAsync(),
            Balances = await query.OrderBy(b => b.Location.LocationCode).ThenBy(b => b.Item.PartNumber).ToListAsync()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitOpname(int balanceId, int physicalQty, string reason, string operatorName, string? redirectLocation)
    {
        var (ok, msg) = await _svc.AdjustStockAsync(balanceId, physicalQty, reason, operatorName);
        if (ok)
            TempData["Success"] = msg;
        else
            TempData["Error"] = msg;

        return RedirectToAction(nameof(StockOpname), new { location = redirectLocation });
    }
}
