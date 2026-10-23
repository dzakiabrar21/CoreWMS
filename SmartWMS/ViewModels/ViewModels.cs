using System.ComponentModel.DataAnnotations;
using SmartWMS.Models;

namespace SmartWMS.ViewModels;

public class StoringCreateVm
{
    [Required, Display(Name = "Part Number")] public int ItemId { get; set; }
    [Required, Display(Name = "Lokasi")] public int LocationId { get; set; }
    [Required, StringLength(50), Display(Name = "Nomor Lot")] public string LotNumber { get; set; } = "";
    [Required, Display(Name = "Tanggal Produksi")] public DateOnly ProductionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Required, Range(0.01, 99.99), Display(Name = "Voltase Terukur (V)")] public decimal MeasuredVoltage { get; set; } = 12.60m;
    [Required, Range(1, 100000)] public int Qty { get; set; }

    public List<Item> Items { get; set; } = new();
    public List<StorageLocation> Locations { get; set; } = new();
    public List<StoringBalance> Recent { get; set; } = new();
}

public class DeliveryCreateVm
{
    [Required, StringLength(100), Display(Name = "Pelanggan")] public string CustomerName { get; set; } = "";
    [Required, Display(Name = "Tanggal Muat")] public DateOnly DeliveryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Required] public string Ritase { get; set; } = Ritases.All[0];
    public List<DeliveryLineVm> Lines { get; set; } = new() { new() };
    public List<Item> Items { get; set; } = new();
    public Dictionary<string, int> RitaseLoad { get; set; } = new();
}
public class DeliveryLineVm
{
    public int ItemId { get; set; }
    public int Qty { get; set; }
}

public record FifoRow(string PartNumber, string LotNumber, string Location, DateOnly ProductionDate, int Quantity, string Status);

public record ScanLine(int ItemId, string PartNumber, string ItemName, int Requested, int Loaded);
public record ScanResult(bool Success, string Message, bool Completed, string DoStatus, List<ScanLine> Lines);

public class ScanLoadingVm
{
    public Delivery Delivery { get; set; } = null!;
    public List<ScanLine> Lines { get; set; } = new();
    public List<FifoRow> Fifo { get; set; } = new();
}

public class DashboardVm
{
    public int TotalCapacity { get; set; }
    public int TotalQty { get; set; }
    public double OccupancyPct => TotalCapacity == 0 ? 0 : Math.Round(TotalQty * 100.0 / TotalCapacity, 1);
    public int AgingCount { get; set; }
    public int TodayDoCount { get; set; }
    public int Normal { get; set; }
    public int Aging { get; set; }
    public int LowVoltage { get; set; }
    public List<LocationTile> Tiles { get; set; } = new();
}
public record LocationTile(string Code, string Zone, int Qty, int Max, string Color);
