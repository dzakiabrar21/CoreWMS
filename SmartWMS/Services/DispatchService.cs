using Microsoft.EntityFrameworkCore;
using SmartWMS.Data;
using SmartWMS.Models;
using SmartWMS.ViewModels;

namespace SmartWMS.Services;

public interface IDispatchService
{
    const int MaxDoPerRitase = 5;   // batas Heijunka: maks DO per jendela ritase per hari (ubah sesuai kebutuhan)
    Task<Dictionary<string, int>> GetRitaseLoadAsync(DateOnly date);
    Task<(bool Ok, string Message, int? Id)> CreateAsync(DeliveryCreateVm vm);
    Task<List<FifoRow>> GetFifoRecommendationAsync(int itemId, int qty);
    Task<ScanLoadingVm?> GetScanVmAsync(int deliveryId);
    Task<ScanResult> ScanAsync(int deliveryId, string barcode, int qty = 1);
}

public class DispatchService : IDispatchService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inv;
    public DispatchService(AppDbContext db, IInventoryService inv) { _db = db; _inv = inv; }

    public async Task<Dictionary<string, int>> GetRitaseLoadAsync(DateOnly date)
    {
        var counts = await _db.Deliveries.Where(d => d.DeliveryDate == date && d.Status != DoStatus.Cancelled)
            .GroupBy(d => d.Ritase).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        return Ritases.All.ToDictionary(r => r, r => counts.FirstOrDefault(c => c.Key == r)?.N ?? 0);
    }

    public async Task<(bool Ok, string Message, int? Id)> CreateAsync(DeliveryCreateVm vm)
    {
        var lines = vm.Lines.Where(l => l.ItemId > 0 && l.Qty > 0).GroupBy(l => l.ItemId)
            .Select(g => new DeliveryLineVm { ItemId = g.Key, Qty = g.Sum(x => x.Qty) }).ToList();
        if (lines.Count == 0) return (false, "Minimal satu item dengan qty > 0.", null);
        if (!Ritases.All.Contains(vm.Ritase)) return (false, "Ritase tidak valid.", null);

        using var tx = await _db.Database.BeginTransactionAsync();
        var load = await GetRitaseLoadAsync(vm.DeliveryDate);
        if (load[vm.Ritase] >= IDispatchService.MaxDoPerRitase)
            return (false, $"{vm.Ritase} pada tanggal tersebut penuh ({IDispatchService.MaxDoPerRitase} DO). Pilih ritase lain (Heijunka).", null);

        await _inv.RefreshStatusesAsync();
        foreach (var l in lines)
        {
            var avail = (await _inv.GetFifoCandidatesAsync(l.ItemId)).Sum(b => b.Quantity);
            var reserved = await _db.DeliveryDetails
                .Where(d => d.ItemId == l.ItemId && (d.Delivery.Status == DoStatus.Planned || d.Delivery.Status == DoStatus.InProgress))
                .SumAsync(d => d.QtyRequested);
            if (avail - reserved < l.Qty)
                return (false, $"Stok tersedia tidak cukup untuk item #{l.ItemId}. Tersedia (di luar DO aktif lain): {Math.Max(0, avail - reserved)}.", null);
        }

        var prefix = $"DO-{vm.DeliveryDate:yyyyMM}-";
        var last = await _db.Deliveries.Where(d => d.DONumber.StartsWith(prefix)).OrderByDescending(d => d.DONumber)
            .Select(d => d.DONumber).FirstOrDefaultAsync();
        var seq = last == null ? 1 : int.Parse(last[prefix.Length..]) + 1;

        var delivery = new Delivery
        {
            DONumber = $"{prefix}{seq:000}", CustomerName = vm.CustomerName.Trim(),
            DeliveryDate = vm.DeliveryDate, Ritase = vm.Ritase, Status = DoStatus.Planned,
            Details = lines.Select(l => new DeliveryDetail { ItemId = l.ItemId, QtyRequested = l.Qty }).ToList()
        };
        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return (true, $"{delivery.DONumber} dibuat.", delivery.Id);
    }

    public async Task<List<FifoRow>> GetFifoRecommendationAsync(int itemId, int qty)
    {
        await _inv.RefreshStatusesAsync();
        var rows = new List<FifoRow>();
        var need = qty;
        foreach (var b in await _inv.GetFifoCandidatesAsync(itemId))
        {
            if (need <= 0) break;
            var take = Math.Min(b.Quantity, need);
            rows.Add(new FifoRow(b.Item.PartNumber, b.LotNumber, b.Location.LocationCode, b.ProductionDate, take, b.Status));
            need -= take;
        }
        return rows;
    }

    private static List<ScanLine> ToLines(Delivery d) =>
        d.Details.Select(x => new ScanLine(x.ItemId, x.Item.PartNumber, x.Item.ItemName, x.QtyRequested, x.QtyLoaded)).ToList();

    public async Task<ScanLoadingVm?> GetScanVmAsync(int id)
    {
        var d = await _db.Deliveries.Include(x => x.Details).ThenInclude(x => x.Item).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return null;
        var fifo = new List<FifoRow>();
        foreach (var det in d.Details)
            fifo.AddRange(await GetFifoRecommendationAsync(det.ItemId, Math.Max(det.QtyRequested - det.QtyLoaded, 0)));
        return new ScanLoadingVm { Delivery = d, Lines = ToLines(d), Fifo = fifo };
    }

    public async Task<ScanResult> ScanAsync(int id, string barcode, int qty = 1)
    {
        using var tx = await _db.Database.BeginTransactionAsync();
        var d = await _db.Deliveries.Include(x => x.Details).ThenInclude(x => x.Item).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return new(false, "DO tidak ditemukan.", false, "", new());

        ScanResult Fail(string msg) => new(false, msg, false, d.Status, ToLines(d));

        if (d.Status != DoStatus.Planned && d.Status != DoStatus.InProgress)
            return Fail($"DO berstatus {d.Status}, tidak bisa dimuat.");
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0 || qty < 1) return Fail("Barcode kosong atau qty tidak valid.");

        int? itemId = null;
        var detByPart = d.Details.FirstOrDefault(x => x.Item.PartNumber.Equals(barcode, StringComparison.OrdinalIgnoreCase));
        if (detByPart != null) itemId = detByPart.ItemId;
        else
        {
            var lot = await _db.StoringBalances.Include(b => b.Location)
                .Where(b => b.LotNumber == barcode.ToUpper() && b.Quantity > 0).ToListAsync();
            var lotRow = lot.FirstOrDefault(b => d.Details.Any(x => x.ItemId == b.ItemId));
            if (lotRow == null) return Fail($"Barcode '{barcode}' TIDAK COCOK dengan item di DO ini.");
            if (lotRow.Status == StockStatus.LowVoltage) return Fail($"Lot {lotRow.LotNumber} berstatus LOW_VOLTAGE, dilarang dimuat.");

            // Poka-Yoke FIFO: lot yang dipindai harus yang tertua
            var oldest = (await _inv.GetFifoCandidatesAsync(lotRow.ItemId)).FirstOrDefault();
            if (oldest != null && lotRow.ProductionDate > oldest.ProductionDate)
                return Fail($"Bukan lot FIFO! Ambil {oldest.LotNumber} di {oldest.Location.LocationCode} dahulu.");
            itemId = lotRow.ItemId;
        }

        var det = d.Details.First(x => x.ItemId == itemId);
        if (det.QtyLoaded + qty > det.QtyRequested)
            return Fail($"Qty {det.Item.PartNumber} sudah penuh ({det.QtyLoaded}/{det.QtyRequested}).");

        det.QtyLoaded += qty;
        det.IsVerified = det.QtyLoaded == det.QtyRequested;
        if (d.Status == DoStatus.Planned) d.Status = DoStatus.InProgress;

        var completed = d.Details.All(x => x.IsVerified);
        try
        {
            if (completed)
            {
                d.Status = DoStatus.Completed;
                foreach (var x in d.Details) await _inv.DeductFifoAsync(x.ItemId, x.QtyRequested);
            }
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return new(false, ex is InvalidOperationException ? ex.Message : "Konflik data stok, ulangi scan.", false, DoStatus.InProgress, new());
        }
        return new(true, completed ? "DO SELESAI. Stok telah dipotong." : $"OK: {det.Item.PartNumber} {det.QtyLoaded}/{det.QtyRequested}",
            completed, d.Status, ToLines(d));
    }
}
