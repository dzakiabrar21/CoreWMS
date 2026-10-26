using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWMS.Data;
using SmartWMS.Models;
using SmartWMS.Services;
using SmartWMS.ViewModels;

namespace SmartWMS.Controllers;

public class ReportController : Controller
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inv;

    public ReportController(AppDbContext db, IInventoryService inv)
    {
        _db = db;
        _inv = inv;
    }

    [HttpGet]
    public async Task<IActionResult> SubLot(string? part, string? status, string? location, string? aging)
    {
        await _inv.RefreshStatusesAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _db.StoringBalances
            .Include(b => b.Item)
            .Include(b => b.Location)
            .Where(b => b.Quantity > 0)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(part))
            query = query.Where(b => b.Item.PartNumber == part);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(b => b.Location.LocationCode == location);

        var allList = await query.ToListAsync();

        if (aging == "AGING")
            allList = allList.Where(b => today.DayNumber - b.ProductionDate.DayNumber > b.Item.MaxShelfLifeDays).ToList();
        else if (aging == "WARNING")
            allList = allList.Where(b => today.DayNumber - b.ProductionDate.DayNumber >= 60 && today.DayNumber - b.ProductionDate.DayNumber <= b.Item.MaxShelfLifeDays).ToList();
        else if (aging == "FRESH")
            allList = allList.Where(b => today.DayNumber - b.ProductionDate.DayNumber < 60).ToList();

        var rows = allList
            .OrderBy(b => b.ProductionDate)
            .ThenBy(b => b.ReceivedAt)
            .Select(b => new SubLotRowVm
            {
                Id = b.Id,
                PartNumber = b.Item.PartNumber,
                ItemName = b.Item.ItemName,
                LotNumber = b.LotNumber,
                LocationCode = b.Location.LocationCode,
                ZoneType = b.Location.ZoneType,
                ProductionDate = b.ProductionDate,
                AgingDays = today.DayNumber - b.ProductionDate.DayNumber,
                MaxShelfLifeDays = b.Item.MaxShelfLifeDays,
                MeasuredVoltage = b.MeasuredVoltage,
                StandardVoltage = b.Item.StandardVoltage,
                MinVoltageThreshold = b.Item.MinVoltageThreshold,
                Quantity = b.Quantity,
                Status = b.Status,
                ReceivedAt = b.ReceivedAt
            }).ToList();

        var vm = new SubLotReportVm
        {
            SelectedPart = part,
            SelectedStatus = status,
            SelectedLocation = location,
            AgingFilter = aging,
            TotalQty = rows.Sum(r => r.Quantity),
            TotalLots = rows.Count,
            TotalAgingQty = rows.Where(r => r.Status == StockStatus.Aging).Sum(r => r.Quantity),
            TotalLowVoltageQty = rows.Where(r => r.Status == StockStatus.LowVoltage).Sum(r => r.Quantity),
            Items = await _db.Items.OrderBy(i => i.PartNumber).ToListAsync(),
            Locations = await _db.StorageLocations.Where(l => l.IsActive).OrderBy(l => l.LocationCode).ToListAsync(),
            Rows = rows
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? part, string? status, string? location, string? aging)
    {
        await _inv.RefreshStatusesAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _db.StoringBalances
            .Include(b => b.Item)
            .Include(b => b.Location)
            .Where(b => b.Quantity > 0)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(part)) query = query.Where(b => b.Item.PartNumber == part);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(b => b.Status == status);
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(b => b.Location.LocationCode == location);

        var allList = await query.ToListAsync();

        if (aging == "AGING")
            allList = allList.Where(b => today.DayNumber - b.ProductionDate.DayNumber > b.Item.MaxShelfLifeDays).ToList();
        else if (aging == "WARNING")
            allList = allList.Where(b => today.DayNumber - b.ProductionDate.DayNumber >= 60 && today.DayNumber - b.ProductionDate.DayNumber <= b.Item.MaxShelfLifeDays).ToList();
        else if (aging == "FRESH")
            allList = allList.Where(b => today.DayNumber - b.ProductionDate.DayNumber < 60).ToList();

        var rows = allList.OrderBy(b => b.ProductionDate).ThenBy(b => b.ReceivedAt).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("No,Part Number,Nama Baterai,No Lot,Lokasi,Zona,Tgl Produksi,Aging (Hari),Max Shelf Life (Hari),Voltase Terukur (V),Std Voltase (V),Min Voltase (V),Qty (Unit),Status Mutu,Waktu Masuk");

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var ageDays = today.DayNumber - r.ProductionDate.DayNumber;
            sb.AppendLine($"{i + 1},\"{r.Item.PartNumber}\",\"{r.Item.ItemName}\",\"{r.LotNumber}\",\"{r.Location.LocationCode}\",\"{r.Location.ZoneType}\",{r.ProductionDate:yyyy-MM-dd},{ageDays},{r.Item.MaxShelfLifeDays},{r.MeasuredVoltage:F2},{r.Item.StandardVoltage:F2},{r.Item.MinVoltageThreshold:F2},{r.Quantity},\"{r.Status}\",{r.ReceivedAt:yyyy-MM-dd HH:mm:ss}");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);

        return File(bytes, "text/csv", $"SubLot_Battery_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    }
}
