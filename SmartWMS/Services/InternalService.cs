using Microsoft.EntityFrameworkCore;
using SmartWMS.Data;
using SmartWMS.Models;

namespace SmartWMS.Services;

public interface IInternalService
{
    Task<(bool Ok, string Message)> TransferStockAsync(int balanceId, int targetLocationId, int qty, string operatorName, string notes);
    Task<(bool Ok, string Message)> AdjustStockAsync(int balanceId, int physicalQty, string reason, string operatorName);
}

public class InternalService : IInternalService
{
    private readonly AppDbContext _db;

    public InternalService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Ok, string Message)> TransferStockAsync(int balanceId, int targetLocationId, int qty, string operatorName, string notes)
    {
        if (qty <= 0) return (false, "Jumlah unit transfer harus lebih dari 0.");

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var sourceBal = await _db.StoringBalances
                .Include(b => b.Item)
                .Include(b => b.Location)
                .FirstOrDefaultAsync(b => b.Id == balanceId);

            if (sourceBal == null || sourceBal.Quantity < qty)
                return (false, "Stok asal tidak mencukupi untuk dipindahkan.");

            var targetLoc = await _db.StorageLocations.FindAsync(targetLocationId);
            if (targetLoc == null || !targetLoc.IsActive)
                return (false, "Lokasi tujuan tidak ditemukan atau tidak aktif.");

            if (sourceBal.LocationId == targetLocationId)
                return (false, "Lokasi tujuan tidak boleh sama dengan lokasi asal.");

            if (targetLoc.CurrentQty + qty > targetLoc.MaxCapacity)
                return (false, $"Kapasitas {targetLoc.LocationCode} tidak mencukupi (sisa slot: {targetLoc.MaxCapacity - targetLoc.CurrentQty}, dibutuhkan: {qty}).");

            // 1. Kurangi lokasi & saldo asal
            sourceBal.Quantity -= qty;
            sourceBal.Location.CurrentQty -= qty;

            // 2. Tambahkan ke saldo di lokasi tujuan (gabungkan jika ada lot yang sama)
            var targetBal = await _db.StoringBalances
                .FirstOrDefaultAsync(b => b.LocationId == targetLocationId && b.ItemId == sourceBal.ItemId && b.LotNumber == sourceBal.LotNumber);

            if (targetBal != null)
            {
                targetBal.Quantity += qty;
            }
            else
            {
                _db.StoringBalances.Add(new StoringBalance
                {
                    ItemId = sourceBal.ItemId,
                    LocationId = targetLoc.Id,
                    LotNumber = sourceBal.LotNumber,
                    ProductionDate = sourceBal.ProductionDate,
                    ExpiryDate = sourceBal.ExpiryDate,
                    MeasuredVoltage = sourceBal.MeasuredVoltage,
                    Quantity = qty,
                    Status = sourceBal.Status,
                    ReceivedAt = sourceBal.ReceivedAt
                });
            }
            targetLoc.CurrentQty += qty;

            // 3. Catat di Buku Besar Mutasi (StockMovement Ledger)
            _db.StockMovements.Add(new StockMovement
            {
                MovementType = MovementType.Transfer,
                ItemId = sourceBal.ItemId,
                FromLocationId = sourceBal.LocationId,
                ToLocationId = targetLoc.Id,
                LotNumber = sourceBal.LotNumber,
                Quantity = qty,
                ReferenceNumber = $"TRF-{DateTime.Now:yyyyMMdd-HHmm}",
                Notes = string.IsNullOrWhiteSpace(notes) ? $"Relokasi dari {sourceBal.Location.LocationCode} ke {targetLoc.LocationCode}" : notes,
                OperatorName = string.IsNullOrWhiteSpace(operatorName) ? "OPERATOR" : operatorName,
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, $"Berhasil memindahkan {qty} unit {sourceBal.Item.PartNumber} ke {targetLoc.LocationCode}.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Gagal melakukan transfer: {ex.Message}");
        }
    }

    public async Task<(bool Ok, string Message)> AdjustStockAsync(int balanceId, int physicalQty, string reason, string operatorName)
    {
        if (physicalQty < 0) return (false, "Jumlah fisik tidak boleh negatif.");

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var bal = await _db.StoringBalances
                .Include(b => b.Item)
                .Include(b => b.Location)
                .FirstOrDefaultAsync(b => b.Id == balanceId);

            if (bal == null) return (false, "Data stok tidak ditemukan.");

            int variance = physicalQty - bal.Quantity;
            if (variance == 0) return (true, "Stok fisik cocok dengan sistem. Tidak ada selisih.");

            // Update saldo dan kapasitas rak
            bal.Quantity = physicalQty;
            bal.Location.CurrentQty += variance;

            // Catat ke StockMovement
            _db.StockMovements.Add(new StockMovement
            {
                MovementType = MovementType.Adjustment,
                ItemId = bal.ItemId,
                ToLocationId = bal.LocationId,
                LotNumber = bal.LotNumber,
                Quantity = Math.Abs(variance),
                ReferenceNumber = $"OPNAME-{DateTime.Now:yyyyMMdd-HHmm}",
                Notes = $"Stock Opname Variance: {(variance > 0 ? "+" : "")}{variance} unit. Alasan: {reason}",
                OperatorName = string.IsNullOrWhiteSpace(operatorName) ? "AUDITOR" : operatorName,
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, $"Stock Opname disetujui. Selisih {(variance > 0 ? "+" : "")}{variance} unit telah direkonsiliasi.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, $"Gagal menyimpan opname: {ex.Message}");
        }
    }
}
