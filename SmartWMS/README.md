# SmartWMS (ASP.NET Core 8 MVC)

## Menjalankan
1. Ubah connection string di `appsettings.json`.
2. `dotnet tool install --global dotnet-ef`
3. `dotnet ef migrations add InitialCreate`
4. `dotnet run` (otomatis Migrate + seed 6 rak, 2 floor, 4 item, sample stok).

## Keputusan desain di luar PRD
- Konkurensi: `RowVersion` pada `StorageLocations` dan `StoringBalances`.
- Prioritas status: LOW_VOLTAGE > AGING_ALERT. Status dihitung ulang tiap dashboard / pembuatan DO.
- Heijunka: maks `IDispatchService.MaxDoPerRitase` (=5) DO per ritase per hari.
- Stok dicadangkan: DO baru ditolak jika stok FIFO-eligible dikurangi DO aktif lain tidak cukup.
- Scan LotNumber harus lot tertua (FIFO); lot LOW_VOLTAGE ditolak. Stok dipotong FIFO saat DO COMPLETED.
- Endpoint `Scan` belum dilindungi antiforgery/auth; tambahkan sebelum produksi.
