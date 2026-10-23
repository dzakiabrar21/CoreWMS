using Microsoft.EntityFrameworkCore;
using SmartWMS.Data;
using SmartWMS.Models;
using SmartWMS.ViewModels;

namespace SmartWMS.Services;

public interface IInventoryService
{
    Task<(bool Ok, string Message)> StoreAsync(StoringCreateVm vm);
    Task RefreshStatusesAsync();
    Task<List<StoringBalance>> GetFifoCandidatesAsync(int itemId);
    Task DeductFifoAsync(int itemId, int qty);   // WAJIB dipanggil di dalam transaksi pemanggil
    Task<DashboardVm> GetDashboardAsync();
    static string ComputeStatus(Item item, DateOnly prod, decimal voltage, DateOnly today)
    {
        // LOW_VOLTAGE diprioritaskan (risiko kimia lebih serius dari umur simpan)
        if (voltage < item.MinVoltageThreshold) return StockStatus.LowVoltage;
        if (today.DayNumber - prod.DayNumber > item.MaxShelfLifeDays) return StockStatus.Aging;
        return StockStatus.Normal;
    }
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    public InventoryService(AppDbContext db) => _db = db;

    public async Task<(bool Ok, string Message)> StoreAsync(StoringCreateVm vm)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var loc = await _db.StorageLocations.FindAsync(vm.LocationId);
            var item = await _db.Items.FindAsync(vm.ItemId);
            if (loc == null || !loc.IsActive) return (false, "Lokasi tidak ditemukan atau tidak aktif.");
            if (item == null) return (false, "Item tidak ditemukan.");
            if (vm.Qty <= 0) return (false, "Qty harus lebih dari 0.");
            if (loc.CurrentQty + vm.Qty > loc.MaxCapacity)
                return (false, $"Kapasitas {loc.LocationCode} terlampaui. Sisa: {loc.MaxCapacity - loc.CurrentQty}, input: {vm.Qty}.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            var status = IInventoryService.ComputeStatus(item, vm.ProductionDate, vm.MeasuredVoltage, today);

            _db.StoringBalances.Add(new StoringBalance
            {
                ItemId = item.Id, LocationId = loc.Id, LotNumber = vm.LotNumber.Trim().ToUpper(),
                ProductionDate = vm.ProductionDate, MeasuredVoltage = vm.MeasuredVoltage,
                Quantity = vm.Qty, Status = status, ReceivedAt = DateTime.Now
            });
            loc.CurrentQty += vm.Qty;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, $"Tersimpan. Status stok: {status}.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Data lokasi baru saja diubah operator lain. Silakan coba lagi.");
        }
    }

    // Aging bertambah seiring waktu, jadi status dihitung ulang, bukan hanya saat inbound.
    public async Task RefreshStatusesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = await _db.StoringBalances.Include(b => b.Item).Where(b => b.Quantity > 0).ToListAsync();
        var changed = false;
        foreach (var r in rows)
        {
            var s = IInventoryService.ComputeStatus(r.Item, r.ProductionDate, r.MeasuredVoltage, today);
            if (s != r.Status) { r.Status = s; changed = true; }
        }
        if (changed) await _db.SaveChangesAsync();
    }

    public async Task<List<StoringBalance>> GetFifoCandidatesAsync(int itemId) =>
        await _db.StoringBalances.Include(b => b.Location).Include(b => b.Item)
            .Where(b => b.ItemId == itemId && b.Quantity > 0 && b.Status != StockStatus.LowVoltage)
            .OrderBy(b => b.ProductionDate).ThenBy(b => b.ReceivedAt).ToListAsync();

    public async Task DeductFifoAsync(int itemId, int qty)
    {
        var remaining = qty;
        foreach (var b in await GetFifoCandidatesAsync(itemId))
        {
            if (remaining == 0) break;
            var take = Math.Min(b.Quantity, remaining);
            b.Quantity -= take;
            b.Location.CurrentQty -= take;
            remaining -= take;
        }
        if (remaining > 0)
            throw new InvalidOperationException($"Stok tidak cukup untuk item #{itemId}, kurang {remaining} unit.");
    }

    public async Task<DashboardVm> GetDashboardAsync()
    {
        await RefreshStatusesAsync();
        var locs = await _db.StorageLocations.Include(l => l.Balances).Where(l => l.IsActive).OrderBy(l => l.LocationCode).ToListAsync();
        var bal = await _db.StoringBalances.Where(b => b.Quantity > 0).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        return new DashboardVm
        {
            TotalCapacity = locs.Sum(l => l.MaxCapacity),
            TotalQty = locs.Sum(l => l.CurrentQty),
            AgingCount = bal.Count(b => b.Status == StockStatus.Aging),
            TodayDoCount = await _db.Deliveries.CountAsync(d => d.DeliveryDate == today && d.Status != DoStatus.Cancelled),
            Normal = bal.Where(b => b.Status == StockStatus.Normal).Sum(b => b.Quantity),
            Aging = bal.Where(b => b.Status == StockStatus.Aging).Sum(b => b.Quantity),
            LowVoltage = bal.Where(b => b.Status == StockStatus.LowVoltage).Sum(b => b.Quantity),
            Tiles = locs.Select(l => new LocationTile(l.LocationCode, l.ZoneType, l.CurrentQty, l.MaxCapacity,
                l.Balances.Any(b => b.Quantity > 0 && b.Status != StockStatus.Normal) ? "danger"
                : l.MaxCapacity > 0 && l.CurrentQty * 100 / l.MaxCapacity >= 80 ? "warning" : "success")).ToList()
        };
    }
}
