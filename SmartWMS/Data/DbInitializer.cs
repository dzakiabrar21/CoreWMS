using Microsoft.EntityFrameworkCore;
using SmartWMS.Models;

namespace SmartWMS.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // 1. Seed Warehouses if empty
        if (!await db.Warehouses.AnyAsync())
        {
            db.Warehouses.AddRange(
                new Warehouse { Code = "WH-KRW-01", Name = "Pabrik Manufaktur Karawang", Address = "Kawasan Industri KIIC Karawang" },
                new Warehouse { Code = "WH-JKT-01", Name = "Pusat Distribusi Cakung", Address = "Kawasan Pergudangan Cakung, Jakarta Timur" }
            );
            await db.SaveChangesAsync();
        }

        // 2. Seed Item Categories if empty
        if (!await db.ItemCategories.AnyAsync())
        {
            db.ItemCategories.AddRange(
                new ItemCategory { Code = "BATTERY", Name = "Aki & Baterai Kendaraan", PickingStrategy = "FIFO", RequiresVoltageCheck = true, RequiresExpiryCheck = false },
                new ItemCategory { Code = "AUTO_PARTS", Name = "Suku Cadang & Spareparts", PickingStrategy = "FIFO", RequiresVoltageCheck = false, RequiresExpiryCheck = false },
                new ItemCategory { Code = "FMCG", Name = "FMCG / Makanan & Minuman", PickingStrategy = "FEFO", RequiresVoltageCheck = false, RequiresExpiryCheck = true },
                new ItemCategory { Code = "ELECTRONICS", Name = "Komponen Elektronika", PickingStrategy = "FIFO", RequiresVoltageCheck = false, RequiresExpiryCheck = false }
            );
            await db.SaveChangesAsync();
        }

        var catBattery = await db.ItemCategories.FirstOrDefaultAsync(c => c.Code == "BATTERY");
        var catAuto = await db.ItemCategories.FirstOrDefaultAsync(c => c.Code == "AUTO_PARTS");
        var catFmcg = await db.ItemCategories.FirstOrDefaultAsync(c => c.Code == "FMCG");
        var whMain = await db.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-KRW-01");

        // 3. Seed Items if empty
        if (!await db.Items.AnyAsync())
        {
            var items = new[]
            {
                new Item { PartNumber = "NS40-MF", ItemName = "Aki NS40 Maintenance Free", CategoryId = catBattery?.Id, UOM = "PCS" },
                new Item { PartNumber = "N70-HYB", ItemName = "Aki N70 Hybrid", CategoryId = catBattery?.Id, UOM = "PCS" },
                new Item { PartNumber = "GZ5S",    ItemName = "Aki Motor GZ5S", CategoryId = catBattery?.Id, UOM = "PCS" },
                new Item { PartNumber = "NS60-MF", ItemName = "Aki NS60 Maintenance Free", CategoryId = catBattery?.Id, UOM = "PCS" },
                new Item { PartNumber = "BRK-PAD-01", ItemName = "Brake Pad Front Set", CategoryId = catAuto?.Id, UOM = "SET", MaxShelfLifeDays = 365 },
                new Item { PartNumber = "OIL-FLT-88", ItemName = "Oil Filter Automotive", CategoryId = catAuto?.Id, UOM = "BOX", MaxShelfLifeDays = 720 },
                new Item { PartNumber = "DRK-BVR-250", ItemName = "Isotonic Energy Drink 250ml", CategoryId = catFmcg?.Id, UOM = "CTN", MaxShelfLifeDays = 180 },
            };
            db.Items.AddRange(items);
            await db.SaveChangesAsync();
        }

        // 4. Seed Storage Locations if empty
        if (!await db.StorageLocations.AnyAsync())
        {
            var locs = new[]
            {
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "RACK-A1-01", ZoneType = "RACK", MaxCapacity = 100, Aisle = "A1", Rack = "01" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "RACK-A1-02", ZoneType = "RACK", MaxCapacity = 100, Aisle = "A1", Rack = "02" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "RACK-A2-01", ZoneType = "RACK", MaxCapacity = 100, Aisle = "A2", Rack = "01" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "RACK-A2-02", ZoneType = "RACK", MaxCapacity = 100, Aisle = "A2", Rack = "02" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "RACK-B1-01", ZoneType = "RACK", MaxCapacity = 120, Aisle = "B1", Rack = "01" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "RACK-B2-01", ZoneType = "RACK", MaxCapacity = 120, Aisle = "B2", Rack = "01" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "FLOOR-BLK-01", ZoneType = "FLOOR", MaxCapacity = 300, Aisle = "BLK", Rack = "01" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "FLOOR-BLK-02", ZoneType = "FLOOR", MaxCapacity = 300, Aisle = "BLK", Rack = "02" },
                new StorageLocation { WarehouseId = whMain?.Id, LocationCode = "COLD-RM-01", ZoneType = "COLD_STORAGE", MaxCapacity = 200, Aisle = "CR", Rack = "01" },
            };
            db.StorageLocations.AddRange(locs);
            await db.SaveChangesAsync();
        }

        // 5. Seed Storing Balances & Initial StockMovements if empty
        if (!await db.StoringBalances.AnyAsync())
        {
            var allItems = await db.Items.ToListAsync();
            var allLocs = await db.StorageLocations.ToListAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);

            void Add(Item i, StorageLocation l, string lot, int ageDays, decimal v, int qty, DateOnly? exp = null)
            {
                var status = v > 0 && v < i.MinVoltageThreshold ? StockStatus.LowVoltage
                           : exp.HasValue && today >= exp.Value ? StockStatus.Expired
                           : ageDays > i.MaxShelfLifeDays ? StockStatus.Aging : StockStatus.Normal;

                var bal = new StoringBalance
                {
                    ItemId = i.Id,
                    LocationId = l.Id,
                    LotNumber = lot,
                    ProductionDate = today.AddDays(-ageDays),
                    ExpiryDate = exp ?? today.AddDays(-ageDays + i.MaxShelfLifeDays),
                    MeasuredVoltage = v,
                    Quantity = qty,
                    Status = status,
                    ReceivedAt = DateTime.Now.AddDays(-ageDays)
                };
                db.StoringBalances.Add(bal);
                l.CurrentQty += qty;

                // Add to StockMovement Ledger (Audit Trail)
                db.StockMovements.Add(new StockMovement
                {
                    MovementType = MovementType.Inbound,
                    ItemId = i.Id,
                    ToLocationId = l.Id,
                    LotNumber = lot,
                    Quantity = qty,
                    ReferenceNumber = $"INITIAL-INB-{lot}",
                    Notes = "Saldo Awal Sistem Gudang",
                    OperatorName = "ADMINISTRATOR",
                    CreatedAt = DateTime.Now.AddDays(-ageDays)
                });
            }

            if (allItems.Count >= 4 && allLocs.Count >= 7)
            {
                Add(allItems[0], allLocs[0], "LOT-NS40-A", 40, 12.65m, 60);
                Add(allItems[0], allLocs[1], "LOT-NS40-B", 10, 12.70m, 50);
                Add(allItems[1], allLocs[2], "LOT-N70-A", 100, 12.55m, 30);   // aging
                Add(allItems[1], allLocs[3], "LOT-N70-B", 20, 12.62m, 40);
                Add(allItems[2], allLocs[4], "LOT-GZ5S-A", 15, 12.30m, 25);   // low voltage
                Add(allItems[2], allLocs[6], "LOT-GZ5S-B", 5, 12.68m, 150);
                Add(allItems[3], allLocs[5], "LOT-NS60-A", 30, 12.66m, 90);
                await db.SaveChangesAsync();
            }
        }
    }
}
