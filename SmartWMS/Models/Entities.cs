using System.ComponentModel.DataAnnotations;

namespace SmartWMS.Models;

public static class StockStatus
{
    public const string Normal = "NORMAL",
                        Aging = "AGING_ALERT",
                        LowVoltage = "LOW_VOLTAGE",
                        Expired = "EXPIRED",
                        Quarantine = "QUARANTINE";
}

public static class DoStatus
{
    public const string Planned = "PLANNED",
                        InProgress = "IN_PROGRESS",
                        Completed = "COMPLETED",
                        Cancelled = "CANCELLED";
}

public static class Ritases
{
    public static readonly string[] All = { "Rit-1 (08:00)", "Rit-2 (11:00)", "Rit-3 (14:00)" };
}

public static class MovementType
{
    public const string Inbound = "INBOUND",
                        Outbound = "OUTBOUND",
                        Transfer = "TRANSFER",
                        Adjustment = "ADJUSTMENT";
}

// 1. WAREHOUSE HIERARCHY
public class Warehouse
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = "WH-01";
    [Required, StringLength(100)] public string Name { get; set; } = "Pusat Distribusi Utama";
    [StringLength(200)] public string Address { get; set; } = "Kawasan Industri";
    public bool IsActive { get; set; } = true;
    public List<StorageLocation> Locations { get; set; } = new();
}

// 2. PRODUCT CATEGORY (MULTI-INDUSTRY CONFIGURATION)
public class ItemCategory
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Code { get; set; } = "GENERAL"; // BATTERY, FMCG, ELECTRONICS, AUTO_PARTS
    [Required, StringLength(100)] public string Name { get; set; } = "General Finished Goods";
    [Required, StringLength(20)] public string PickingStrategy { get; set; } = "FIFO"; // FIFO | FEFO | LIFO
    public bool RequiresVoltageCheck { get; set; } = false;
    public bool RequiresExpiryCheck { get; set; } = false;
    public List<Item> Items { get; set; } = new();
}

// 3. UNIVERSAL ITEM / SKU
public class Item
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string PartNumber { get; set; } = ""; // SKU Unik
    [Required, StringLength(100)] public string ItemName { get; set; } = "";
    public int? CategoryId { get; set; }
    public ItemCategory? Category { get; set; }
    [Required, StringLength(20)] public string UOM { get; set; } = "PCS"; // PCS, BOX, PALLET, KG, PACK
    public int MinStockLevel { get; set; } = 10;
    public int MaxStockLevel { get; set; } = 5000;

    // Quality & Shelf life attributes (Universal + Backward Compatible)
    public int MaxShelfLifeDays { get; set; } = 90;
    public decimal StandardVoltage { get; set; } = 12.60m;
    public decimal MinVoltageThreshold { get; set; } = 12.40m;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<StoringBalance> Balances { get; set; } = new();
}

// 4. STORAGE LOCATIONS (MULTI-ZONE)
public class StorageLocation
{
    public int Id { get; set; }
    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    [Required, StringLength(30)] public string LocationCode { get; set; } = "";
    [Required, StringLength(30)] public string ZoneType { get; set; } = "RACK"; // RACK | FLOOR | COLD_STORAGE | BULK | STAGING
    [StringLength(30)] public string Aisle { get; set; } = "";
    [StringLength(30)] public string Rack { get; set; } = "";
    public int MaxCapacity { get; set; }
    public int CurrentQty { get; set; }
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[]? RowVersion { get; set; }
    public List<StoringBalance> Balances { get; set; } = new();
}

// 5. STORING BALANCES (PHYSICAL STOCK PER LOT/EXPIRY)
public class StoringBalance
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int LocationId { get; set; }
    public StorageLocation Location { get; set; } = null!;
    [Required, StringLength(50)] public string LotNumber { get; set; } = "";
    public DateOnly ProductionDate { get; set; }
    public DateOnly? ExpiryDate { get; set; } // Vital for FMCG/Food/Pharma (FEFO)
    public decimal MeasuredVoltage { get; set; }
    public int Quantity { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = StockStatus.Normal;
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
    [Timestamp] public byte[]? RowVersion { get; set; }
}

// 6. DELIVERIES (SURAT JALAN / DELIVERY ORDER)
public class Delivery
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string DONumber { get; set; } = "";
    [Required, StringLength(100)] public string CustomerName { get; set; } = "";
    public DateOnly DeliveryDate { get; set; }
    [Required, StringLength(20)] public string Ritase { get; set; } = "";
    [Required, StringLength(30)] public string Status { get; set; } = DoStatus.Planned;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<DeliveryDetail> Details { get; set; } = new();
}

// 7. DELIVERY DETAILS
public class DeliveryDetail
{
    public int Id { get; set; }
    public int DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int QtyRequested { get; set; }
    public int QtyLoaded { get; set; }
    public bool IsVerified { get; set; }
}

// 8. ENTERPRISE STOCK MOVEMENT LEDGER (AUDIT TRAIL)
public class StockMovement
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string MovementType { get; set; } = "INBOUND";
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int? FromLocationId { get; set; }
    public StorageLocation? FromLocation { get; set; }
    public int? ToLocationId { get; set; }
    public StorageLocation? ToLocation { get; set; }
    [Required, StringLength(50)] public string LotNumber { get; set; } = "";
    public int Quantity { get; set; }
    [StringLength(100)] public string ReferenceNumber { get; set; } = "";
    [StringLength(200)] public string Notes { get; set; } = "";
    [StringLength(50)] public string OperatorName { get; set; } = "OPERATOR";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
