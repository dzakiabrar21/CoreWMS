using Microsoft.EntityFrameworkCore;
using SmartWMS.Models;

namespace SmartWMS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<StoringBalance> StoringBalances => Set<StoringBalance>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryDetail> DeliveryDetails => Set<DeliveryDetail>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Warehouse>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<ItemCategory>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<Item>(e =>
        {
            e.HasIndex(x => x.PartNumber).IsUnique();
            e.Property(x => x.StandardVoltage).HasPrecision(4, 2);
            e.Property(x => x.MinVoltageThreshold).HasPrecision(4, 2);
        });

        b.Entity<StorageLocation>().HasIndex(x => x.LocationCode).IsUnique();

        b.Entity<StoringBalance>(e =>
        {
            e.Property(x => x.MeasuredVoltage).HasPrecision(4, 2);
            e.HasIndex(x => new { x.ItemId, x.Status, x.ProductionDate });
        });

        b.Entity<Delivery>(e =>
        {
            e.HasIndex(x => x.DONumber).IsUnique();
            e.HasIndex(x => new { x.DeliveryDate, x.Ritase });
        });

        b.Entity<StockMovement>(e =>
        {
            e.HasIndex(x => new { x.ItemId, x.CreatedAt });
            e.HasIndex(x => x.ReferenceNumber);
        });
    }
}
