# PRODUCT REQUIREMENTS DOCUMENT (PRD) & SYSTEM SPECIFICATION
## SmartWMS: Manufacturing Finished Goods & Outbound Dispatch Portal

---

## 1. PROJECT OVERVIEW & GOAL
SmartWMS adalah portal web pergudangan manufaktur terpadu (Warehouse Finished Goods & Outbound Dispatch) yang mengadopsi alur operasional industri baterai (terinspirasi dari alur CBI/Omron). 

Sistem ini menggabungkan dua domain utama:
1. **Internal Warehouse Management:** Manajemen penyimpanan rak bertingkat (*Rack*) & lantai (*Floor*), pemantauan kualitas kimia baterai (*Aging Days* & *Measured Voltage*), serta mutasi stok.
2. **Outbound Dispatch Logistics:** Penjadwalan pengiriman berbasis perataan beban (*Heijunka Ritase*), alokasi stok otomatis berbasis *FIFO*, dan verifikasi muat barang anti-salah (*Poka-Yoke Loading Verification*).

### Tech Stack Standards
- **Framework:** ASP.NET Core MVC (.NET 8)
- **Language:** C# 12
- **ORM & Database:** Entity Framework Core 8 dengan Microsoft SQL Server (Code-First)
- **Frontend:** HTML5, CSS3, Bootstrap 5, JavaScript, jQuery 3.7+
- **Visuals & Charts:** Chart.js (untuk occupancy dan ritase dispatch tracking)
- **Architecture Pattern:** Clean MVC (Controllers, Models/ViewModels, Data/DbContext, Services, Views)

---

## 2. DATABASE SCHEMA & DATA DICTIONARY

### 2.1. Table: `Items` (Master Baterai / Finished Goods)
| Column | Type | Nullable | Description / Rules |
| :--- | :--- | :--- | :--- |
| `Id` | int | No (PK) | Auto-increment primary key |
| `PartNumber` | nvarchar(50) | No | SKU unik produk (misal: "NS40-MF", "N70-HYB", "GZ5S") |
| `ItemName` | nvarchar(100) | No | Nama lengkap baterai / aki |
| `StandardVoltage` | decimal(4,2) | No | Ambang batas voltase normal (default: 12.60 V) |
| `MinVoltageThreshold` | decimal(4,2) | No | Batas kritis voltase rendah (default: 12.40 V) |
| `MaxShelfLifeDays` | int | No | Batas maksimal simpan tanpa cas ulang (default: 90 hari) |
| `CreatedAt` | datetime2 | No | Waktu dibuat |

### 2.2. Table: `StorageLocations` (Master Alamat Rak & Floor)
| Column | Type | Nullable | Description / Rules |
| :--- | :--- | :--- | :--- |
| `Id` | int | No (PK) | Auto-increment primary key |
| `LocationCode` | nvarchar(20) | No | Kode unik slot (misal: "RACK-A1-01", "FLOOR-BLK-01") |
| `ZoneType` | nvarchar(20) | No | Enum/string: "RACK" atau "FLOOR" |
| `MaxCapacity` | int | No | Kapasitas palet/unit maksimal |
| `CurrentQty` | int | No | Jumlah akumulasi baterai saat ini |
| `IsActive` | bit | No | Status aktif lokasi |

### 2.3. Table: `StoringBalances` (Stok Fisik Aktif per Lot & Alamat)
| Column | Type | Nullable | Description / Rules |
| :--- | :--- | :--- | :--- |
| `Id` | int | No (PK) | Auto-increment primary key |
| `ItemId` | int | No (FK) | Relasi ke `Items.Id` |
| `LocationId` | int | No (FK) | Relasi ke `StorageLocations.Id` |
| `LotNumber` | nvarchar(50) | No | Nomor batch produksi unik |
| `ProductionDate` | date | No | Tanggal produksi (dasar kalkulasi Aging & FIFO) |
| `MeasuredVoltage` | decimal(4,2) | No | Nilai voltase yang diukur saat inbound |
| `Quantity` | int | No | Sisa unit fisik yang tersedia |
| `Status` | nvarchar(30) | No | "NORMAL", "AGING_ALERT" (>90 hari), "LOW_VOLTAGE" (<12.4V) |
| `ReceivedAt` | datetime2 | No | Timestamp masuk gudang |

### 2.4. Table: `Deliveries` (Surat Jalan / Delivery Order)
| Column | Type | Nullable | Description / Rules |
| :--- | :--- | :--- | :--- |
| `Id` | int | No (PK) | Auto-increment primary key |
| `DONumber` | nvarchar(50) | No | Nomor Delivery Order unik (misal: "DO-202610-001") |
| `CustomerName` | nvarchar(100) | No | Nama pelanggan (misal: "Astra Otoparts", "PDC Karawang") |
| `DeliveryDate` | date | No | Tanggal rencana muat |
| `Ritase` | nvarchar(20) | No | Pilihan jendela waktu: "Rit-1 (08:00)", "Rit-2 (11:00)", "Rit-3 (14:00)" |
| `Status` | nvarchar(30) | No | "PLANNED", "IN_PROGRESS", "COMPLETED", "CANCELLED" |
| `CreatedAt` | datetime2 | No | Timestamp pembuatan rencana |

### 2.5. Table: `DeliveryDetails` (Detail Item per Surat Jalan)
| Column | Type | Nullable | Description / Rules |
| :--- | :--- | :--- | :--- |
| `Id` | int | No (PK) | Auto-increment primary key |
| `DeliveryId` | int | No (FK) | Relasi ke `Deliveries.Id` |
| `ItemId` | int | No (FK) | Relasi ke `Items.Id` |
| `QtyRequested` | int | No | Jumlah unit pesanan |
| `QtyLoaded` | int | No | Akumulasi unit yang lolos scan verifikasi |
| `IsVerified` | bit | No | True jika QtyLoaded == QtyRequested |

---

## 3. BUSINESS RULES & LOGIC SPECIFICATIONS

### 3.1. Inbound & Storing Logic
1. Saat operator menginput baterai baru di form `Checksheet Storing`:
   - Validasi kapasitas lokasi rak/lantai: `CurrentQty + InputQty <= MaxCapacity`. Jika melebihi, tolak transaksi dengan pesan error.
   - Periksa tanggal produksi terhadap tanggal hari ini untuk mendapatkan `AgingDays`:
     - Jika `AgingDays > MaxShelfLifeDays`, set status menjadi `AGING_ALERT`.
     - Jika `MeasuredVoltage < MinVoltageThreshold`, set status menjadi `LOW_VOLTAGE`.
     - Selain itu, status adalah `NORMAL`.
   - Update `CurrentQty` pada tabel `StorageLocations` secara transaksional (`IDbContextTransaction`).

### 3.2. FIFO Picking Allocation Logic (Outbound)
1. Saat pembuatan pengiriman baru:
   - Sistem mencari stok di `StoringBalances` untuk `ItemId` yang diminta dengan syarat `Quantity > 0` dan `Status != 'LOW_VOLTAGE'`.
   - Urutkan data berdasarkan `ProductionDate ASC` (FIFO).
   - Tampilkan rekomendasi lokasi dan nomor lot tertua di layar agar operator tahu rak mana yang harus diambil duluan.

### 3.3. Poka-Yoke Loading Verification (Gate Scan)
1. Di layar `ScanLoading`:
   - Operator memilih nomor DO yang sedang berstatus `PLANNED` atau `IN_PROGRESS`.
   - Operator memindai/mengetik kode barcode barang (`PartNumber` atau `LotNumber`).
   - Logika Poka-Yoke:
     - Jika barcode COCOK dengan item di detail DO dan `QtyLoaded < QtyRequested`:
       - Tambah `QtyLoaded` sebesar 1 (atau jumlah sublot).
       - Tampilkan visual indikator HIJAU dan mainkan audio sukses.
     - Jika barcode TIDAK COCOK atau kuantitas sudah melebihi permintaan:
       - Tolak penambahan, tampilkan visual indikator MERAH, dan mainkan audio error buzzer.
   - Jika semua item dalam DO telah selesai diverifikasi (`QtyLoaded == QtyRequested`), ubah status DO menjadi `COMPLETED` dan potong stok di `StoringBalances` serta `StorageLocations`.

---

## 4. UI/UX & PAGE REQUIREMENTS

1. **Dashboard (`/Home/Index`):**
   - 4 Card Metrik: Total Kapasitas Gudang, Keterisian Saat Ini (%), Total Item Aging Alert, Total DO Ritase Hari Ini.
   - Visual Grid Status Gudang: Menampilkan petak-petak rak (RAK-A1 s/d RAK-B2) dan floor storage dengan kode warna (Hijau = Aman, Oranye = Hampir Penuh, Merah = Berisi Stok Aging/Drop Voltase).
   - Diagram Donat Chart.js: Komposisi status stok (Normal vs Aging vs Low Voltage).

2. **Checksheet Storing (`/Storing/Create`):**
   - Form input ramah operator: Part Number (dropdown), Lokasi Rak/Floor (dropdown dinamis dengan indikator sisa kapasitas), Nomor Lot, Tanggal Produksi, Voltase Terukur, dan Qty.
   - Tabel riwayat transaksi penerimaan terakhir.

3. **Heijunka & Outbound Dispatch (`/Dispatch/Index`):**
   - Tabel pengiriman dikelompokkan berdasarkan tanggal dan tab *Ritase* (Rit-1, Rit-2, Rit-3).
   - Tombol "Buat Rencana Kirim" (Modal / Halaman baru).
   - Tombol "Mulai Poka-Yoke Scan" yang mengarahkan ke terminal verifikasi.

4. **Poka-Yoke Loading Simulator (`/Dispatch/ScanLoading/{id}`):**
   - Input barcode dengan atribut `autofocus` dan auto-submit / on-enter listener.
   - Box indikator besar (Hijau / Merah) untuk feedback langsung ke operator.
   - Audio feedback synthesizer menggunakan HTML5 Web Audio API (tidak butuh file mp3 eksternal).
   - Progress bar interaktif persentase pemuatan per item pesanan.

---

## 5. NON-FUNCTIONAL REQUIREMENTS & CODE HYGIENE
- **Database Transactions:** Semua mutasi stok (inbound dan outbound) WAJIB dibungkus `using var transaction = await _context.Database.BeginTransactionAsync();` untuk mencegah selisih stok.
- **Dependency Injection:** Seluruh kalkulasi FIFO dan mutasi stok diisolasi ke dalam `IInventoryService` dan `IDispatchService`, bukan ditulis bertumpuk di Controller.
- **Data Seeding:** Proyek harus memiliki `DbInitializer.cs` yang otomatis mengisi minimal 6 data rak, 2 data floor, dan 4 master item saat pertama kali dijalankan (`dotnet ef database update`).
- **Responsive & Clean UI:** Gunakan Bootstrap 5 cards, badge, modal, dan alert tanpa dependensi CSS framework tambahan.