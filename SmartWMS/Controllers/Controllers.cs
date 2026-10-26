using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWMS.Data;
using SmartWMS.Models;
using SmartWMS.Services;
using SmartWMS.ViewModels;

namespace SmartWMS.Controllers;

public class HomeController : Controller
{
    private readonly IInventoryService _inv;
    public HomeController(IInventoryService inv) => _inv = inv;

    [HttpGet]
    public async Task<IActionResult> Index() => View("Dashboard", await _inv.GetDashboardAsync());

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard() => View(await _inv.GetDashboardAsync());

    [HttpGet("RackInventory")]
    public async Task<IActionResult> RackInventory() => View(await _inv.GetDashboardAsync());
}

public class StoringController : Controller
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inv;
    public StoringController(AppDbContext db, IInventoryService inv) { _db = db; _inv = inv; }

    private async Task Fill(StoringCreateVm vm)
    {
        vm.Items = await _db.Items.OrderBy(i => i.PartNumber).ToListAsync();
        vm.Locations = await _db.StorageLocations.Where(l => l.IsActive).OrderBy(l => l.LocationCode).ToListAsync();
        vm.Recent = await _db.StoringBalances.Include(b => b.Item).Include(b => b.Location)
            .OrderByDescending(b => b.ReceivedAt).Take(15).ToListAsync();
    }

    [HttpGet] public async Task<IActionResult> Create() { var vm = new StoringCreateVm(); await Fill(vm); return View(vm); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StoringCreateVm vm)
    {
        if (ModelState.IsValid)
        {
            var (ok, msg) = await _inv.StoreAsync(vm);
            if (ok) { TempData["Success"] = msg; return RedirectToAction(nameof(Create)); }
            ModelState.AddModelError("", msg);
        }
        await Fill(vm);
        return View(vm);
    }
}

public class DispatchController : Controller
{
    private readonly AppDbContext _db;
    private readonly IDispatchService _svc;
    public DispatchController(AppDbContext db, IDispatchService svc) { _db = db; _svc = svc; }

    public async Task<IActionResult> Index(DateOnly? date)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.Today);
        ViewBag.Date = d;
        ViewBag.Load = await _svc.GetRitaseLoadAsync(d);
        var list = await _db.Deliveries.Include(x => x.Details).ThenInclude(x => x.Item)
            .Where(x => x.DeliveryDate == d).OrderBy(x => x.DONumber).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Create(DateOnly? date)
    {
        var vm = new DeliveryCreateVm { DeliveryDate = date ?? DateOnly.FromDateTime(DateTime.Today) };
        vm.Items = await _db.Items.OrderBy(i => i.PartNumber).ToListAsync();
        vm.RitaseLoad = await _svc.GetRitaseLoadAsync(vm.DeliveryDate);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DeliveryCreateVm vm)
    {
        if (ModelState.IsValid)
        {
            var (ok, msg, _) = await _svc.CreateAsync(vm);
            if (ok) { TempData["Success"] = msg; return RedirectToAction(nameof(Index), new { date = vm.DeliveryDate.ToString("yyyy-MM-dd") }); }
            ModelState.AddModelError("", msg);
        }
        vm.Items = await _db.Items.OrderBy(i => i.PartNumber).ToListAsync();
        vm.RitaseLoad = await _svc.GetRitaseLoadAsync(vm.DeliveryDate);
        return View(vm);
    }

    [HttpGet] public async Task<IActionResult> Fifo(int itemId, int qty) => Json(await _svc.GetFifoRecommendationAsync(itemId, qty));

    [HttpGet("Dispatch/ScanLoading/{id:int}")]
    public async Task<IActionResult> ScanLoading(int id)
    {
        var vm = await _svc.GetScanVmAsync(id);
        return vm == null ? NotFound() : View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Scan(int id, string barcode, int qty = 1) => Json(await _svc.ScanAsync(id, barcode, qty));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var d = await _db.Deliveries.FindAsync(id);
        if (d != null && d.Status == DoStatus.Planned) { d.Status = DoStatus.Cancelled; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { date = d?.DeliveryDate.ToString("yyyy-MM-dd") });
    }

    [HttpGet("Dispatch/Print/{id:int}")]
    public async Task<IActionResult> Print(int id)
    {
        var d = await _db.Deliveries
            .Include(x => x.Details).ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id);
        return d == null ? NotFound() : View(d);
    }
}
