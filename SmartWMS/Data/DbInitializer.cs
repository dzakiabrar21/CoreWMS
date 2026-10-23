using Microsoft.EntityFrameworkCore;
using SmartWMS.Models;

namespace SmartWMS.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Items.AnyAsync()) return;

        var items = new[]
        {
            new Item { PartNumber = "NS40-MF", ItemName = "Aki NS40 Maintenance Free" },
            new Item { PartNumber = "N70-HYB", ItemName = "Aki N70 Hybrid" },
            new Item { PartNumber = "GZ5S",    ItemName = "Aki Motor GZ5S" },
            new Item { PartNumber = "NS60-MF", ItemName = "Aki NS60 Maintenance Free" },
        };
        db.Items.AddRange(items);

        var locs = new[]
        {
            new StorageLocation { LocationCode = "RACK-A1-01", ZoneType = "RACK",  MaxCapacity = 100 },
            new StorageLocation { LocationCode = "RACK-A1-02", ZoneType = "RACK",  MaxCapacity = 100 },
            new StorageLocation { LocationCode = "RACK-A2-01", ZoneType = "RACK",  MaxCapacity = 100 },
            new StorageLocation { LocationCode = "RACK-A2-02", ZoneType = "RACK",  MaxCapacity = 100 },
            new StorageLocation { LocationCode = "RACK-B1-01", ZoneType = "RACK",  MaxCapacity = 120 },
            new StorageLocation { LocationCode = "RACK-B2-01", ZoneType = "RACK",  MaxCapacity = 120 },
            new StorageLocation { LocationCode = "FLOOR-BLK-01", ZoneType = "FLOOR", MaxCapacity = 300 },
            new StorageLocation { LocationCode = "FLOOR-BLK-02", ZoneType = "FLOOR", MaxCapacity = 300 },
        };
        db.StorageLocations.AddRange(locs);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        void Add(Item i, StorageLocation l, string lot, int ageDays, decimal v, int qty)
        {
            var status = v < i.MinVoltageThreshold ? StockStatus.LowVoltage
                       : ageDays > i.MaxShelfLifeDays ? StockStatus.Aging : StockStatus.Normal;
            db.StoringBalances.Add(new StoringBalance
            {
                ItemId = i.Id, LocationId = l.Id, LotNumber = lot, ProductionDate = today.AddDays(-ageDays),
                MeasuredVoltage = v, Quantity = qty, Status = status
            });
            l.CurrentQty += qty;
        }
        Add(items[0], locs[0], "LOT-NS40-A", 40, 12.65m, 60);
        Add(items[0], locs[1], "LOT-NS40-B", 10, 12.70m, 50);
        Add(items[1], locs[2], "LOT-N70-A", 100, 12.55m, 30);   // aging
        Add(items[1], locs[3], "LOT-N70-B", 20, 12.62m, 40);
        Add(items[2], locs[4], "LOT-GZ5S-A", 15, 12.30m, 25);   // low voltage
        Add(items[2], locs[6], "LOT-GZ5S-B", 5, 12.68m, 150);
        Add(items[3], locs[5], "LOT-NS60-A", 30, 12.66m, 90);
        await db.SaveChangesAsync();
    }
}
