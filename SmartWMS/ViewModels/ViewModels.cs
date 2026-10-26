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

public class SubLotReportVm
{
    public string? SelectedPart { get; set; }
    public string? SelectedStatus { get; set; }
    public string? SelectedLocation { get; set; }
    public string? AgingFilter { get; set; }

    public int TotalQty { get; set; }
    public int TotalLots { get; set; }
    public int TotalAgingQty { get; set; }
    public int TotalLowVoltageQty { get; set; }

    public List<Item> Items { get; set; } = new();
    public List<StorageLocation> Locations { get; set; } = new();
    public List<SubLotRowVm> Rows { get; set; } = new();
}

public class SubLotRowVm
{
    public int Id { get; set; }
    public string PartNumber { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string LotNumber { get; set; } = "";
    public string LocationCode { get; set; } = "";
    public string ZoneType { get; set; } = "";
    public DateOnly ProductionDate { get; set; }
    public int AgingDays { get; set; }
    public int MaxShelfLifeDays { get; set; }
    public decimal MeasuredVoltage { get; set; }
    public decimal StandardVoltage { get; set; }
    public decimal MinVoltageThreshold { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}

public class MasterItemsVm
{
    public List<Item> Items { get; set; } = new();
    public List<ItemCategory> Categories { get; set; } = new();
}

public class MasterLocationsVm
{
    public List<StorageLocation> Locations { get; set; } = new();
    public List<Warehouse> Warehouses { get; set; } = new();
}

public class AuditTrailVm
{
    public string? SelectedType { get; set; }
    public string? SelectedPart { get; set; }
    public List<StockMovement> Movements { get; set; } = new();
    public List<Item> Items { get; set; } = new();
}


