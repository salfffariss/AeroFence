# 🚀 AeroFence - Progress Roadmap

A modern, lightweight, modular desktop container built with **C# (.NET 9 + WPF)** and Windows 11 Fluent design.

---

## 📌 Development Milestones (12 Steps)

### Fase 1: Fondasi Proyek & Data Layer
- [x] **Langkah 1: Setup Proyek .NET 9 WPF & Paket MVVM Toolkit**
  - Inisialisasi arsitektur modular (`Models`, `ViewModels`, `Views`, `Services`, `Styles`).
  - Integrasi paket resmi `CommunityToolkit.Mvvm`.
  - Pemisahan tema visual (`Styles/Theme.xaml`).
  - Status: **SELESAI (0 Errors, 0 Warnings)**.
- [x] **Langkah 2: Kontrak Data Model & JSON Storage Engine**
  - Implementasi `FenceModel`, `TabModel`, `ItemModel`, `PlaytimeStats`, `AppConfig`.
  - Layanan persistensi lokal `JsonStorageService.cs` (portable, atomic write, tanpa registry).
  - Verifikasi otomatis: Auto-generate `Data/fences.json` (2 KB).
  - Status: **SELESAI (0 Errors, 0 Warnings)**.

### Fase 2: Shell Window & Estetika Kaca
- [x] **Langkah 3: Custom Window Chrome**
  - Borderless window, custom titlebar, tombol roll-up, minimize & close.
  - Native Win32 `WM_NCHITTEST` edge/corner resizing (8 arah dengan penyesuaian DPI).
  - Mekanisme Roll-up ala Fences (double click titlebar / tombol ciut ke 36px).
  - Status: **SELESAI (0 Errors, 0 Warnings)**.
- [x] **Langkah 4: Efek Kaca Windows 11 (Mica Alt & Acrylic Blur)**
  - Layanan modular `DwmService.cs` dengan Win32 `DwmSetWindowAttribute`.
  - Akselerasi hardware DWM (`DWMSBT_TRANSIENTWINDOW` Acrylic Blur + Immersive Dark Mode).
  - Integrasi tema frosted glass (`AcrylicTintBrush` & `AcrylicBorderBrush`).
  - Status: **SELESAI (0 Errors, 0 Warnings)**.

### Fase 3: Tab Kontainer & Ekstraksi Ikon
- [x] **Langkah 5: Sistem Tab Kontainer**
  - Implementasi TabBar strip dinamis terikat ke `ObservableCollection<TabModel>`.
  - Pergantian tab aktif (`SelectTabCommand`) secara instan.
  - Tombol tambah tab baru (`+`) & context menu klik kanan untuk hapus tab.
  - Tampilan item kartu shortcut untuk tiap tab aktif (Notepad, Calculator, CMD).
  - Status: **SELESAI (0 Errors, 0 Warnings)**.
- [x] **Langkah 6: Windows Shell Icon Extractor**
  - Layanan modular `ShellIconService.cs` dengan Win32 `SHGetFileInfo` & pembersihan handle `DestroyIcon`.
  - Ekstraksi ikon native resolusi tinggi (.exe, .lnk, .url, Windows system tools).
  - ValueConverter `PathToIconConverter.cs` terikat langsung ke XAML `<Image>`.
  - Caching in-memory `ConcurrentDictionary` untuk performa cepat tanpa re-read disk.
  - Pin Toggle (`📌` / `📍`) + Auto-Rollup on Mouse Leave & Expand on Mouse Enter.
  - Status: **SELESAI (0 Errors, 0 Warnings)**.

### Fase 4: Kanvas Bebas & Drag-and-Drop
- [x] **Langkah 7: Komponen Kartu Ikon & Interaktivitas Desktop**
  - Komponen terpisah `Views/ItemCardView.xaml` (lebar kompak 80px, border transparan default, borderless).
  - Visual selection state: border aksen Fluent (`#9938BDF8`) & background subtle glow saat dipilih.
  - Multi-select support (Ctrl + Click) & klik area kosong kanvas untuk deselect semua item.
  - Eksekusi ganda (Double-Click Launch) via `Process.Start` + auto-increment launch counter & auto-save JSON.
  - Tooltip bersih Opsi B (hanya durasi playtime & jumlah launch, tanpa judul ganda).
  - Dark Fluent Context Menu (🚀 Open, 📂 Open file location, 🗑️ Remove from Fence).
  - TitleBar Ramping (32px): Judul di-center presisi dan transparan untuk drag jendela.
  - Tombol minimize (`-`) diganti tombol pengaturan (`⚙️`) dengan flyout menu:
    - Pilihan ukuran ikon dinamis: **Small** (24px), **Medium** (36px), **Large** (48px) + persistensi JSON.
    - Pilihan sorting instan (Nama A-Z, Playtime, Launch Count).
    - Akses ukuran ikon & sorting juga tersedia via klik kanan kanvas kosong ala Windows Desktop.
  - Status: **SELESAI (0 Errors, 0 Warnings)**.
- [x] **Langkah 8: Internal Drag & Drop (Pergeseran Ikon + Auto-Save)**
  - Pengenalan drag cerdas dengan threshold jarak sistem Windows (`MinimumHorizontalDragDistance`), membedakan klik tunggal, klik ganda, dan drag.
  - Efek visual semi-transparan (`Opacity = 0.45`) saat ikon sedang di-drag.
  - Indikator drop visual (`DropIndicator` border aksen cyan menyala) pada kartu target saat di-hover.
  - Pergeseran urutan instan (`ObservableCollection.Move`) antar ikon dalam tab yang sama.
  - Mendukung pemindahan ikon ke posisi paling akhir saat di-drop di area kosong kanvas.
  - Mendukung transfer ikon antar tab: drag ikon dari tab aktif dan drop ke tombol tab lain di TabBar.
  - Auto-save urutan & penempatan baru secara instan ke `fences.json`.
  - Status: **SELESAI (0 Errors, 0 Warnings)**.
- [x] **Langkah 9: External Drag & Drop (Drop file dari Windows Explorer / Desktop)**
  - Auto-hide tombol aksi titlebar saat Fence ditutup/rolled-up: hanya judul bersih yang tampak mengambang di desktop; tombol muncul kembali dengan transisi halus saat di-hover atau dibuka kembali.
  - Dukungan native `DataFormats.FileDrop` untuk file, shortcut (.lnk, .url), executable (.exe), folder, dan dokumen dari Windows Explorer atau Desktop.
  - Drop ke kanvas: file otomatis ditambahkan ke tab aktif dengan ekstraksi nama bersih, ikon native beresolusi tinggi, dan auto-save JSON.
  - Drop ke tombol tab: file bisa langsung dijatuhkan ke tombol tab tertentu di TabBar untuk organisasi kategori instan.
  - Status: **SELESAI (0 Errors, 0 Warnings)**.

### Fase 5: Fitur Cerdas & Interaktivitas
- [x] **Langkah 10: Type-to-Filter Instant Search**
  - Perbaikan bug context menu melayang: registrasi global lifecycle event handler (`ContextMenu.Opened` / `ContextMenu.Closed`) yang mencegah unpinned Fence menutup otomatis saat context menu sedang aktif.
  - Pencarian tanpa klik (*Zero-Click Type-to-Filter*): cukup arahkan kursor ke Fence lalu ketik apa saja di keyboard, filter langsung aktif seketika.
  - Visual filter cerdas: ikon yang tidak cocok diredupkan (*dimmed* ke 20% opacity), dan hasil pencarian terbaik (*best match*) disorot dengan border cyan menyala (`#38BDF8`).
  - Search Overlay Pill melayang (*floating frosted chip*) di bagian atas kanvas menampilkan kata kunci pencarian, jumlah kecocokan (*match count*), dan tombol `✕` untuk reset.
  - Navigasi keyboard cepat: `Enter` untuk langsung menjalankan item terbaik (*instant launch*), `Esc` / `✕` untuk membersihkan pencarian, dan `Backspace` untuk menghapus karakter.
  - Status: **SELESAI (0 Errors, 0 Warnings)**.
- [x] **Langkah 11: Peluncur Game & Playtime Tracker Engine**
  - Perbaikan bug tombol spasi saat pencarian: `Key.Space` dicegah memicu klik tombol/tab terakhir yang difokuskan, dan secara cerdas dimasukkan ke query pencarian multi-kata (*multi-word search*).
  - Layanan modular `PlaytimeTrackerService.cs`: melacak proses game/aplikasi yang berjalan di latar belakang secara *real-time* via `Process.Exited` & periodic timer (5 detik).
  - Indikator runtime visual ala Steam/GOG: kartu item yang sedang aktif menampilkan badge **`● RUNNING`** berwarna hijau zamrud (*emerald glow*) pada kanvas dan tooltip.
  - Akumulasi playtime otomatis: durasi bermain terhitung presisi dalam detik, terkonversi otomatis ke jam/menit (`FormattedPlaytime`), dan tersimpan aman di `fences.json`.
  - Peningkatan peluncur: peluncuran shortcut (.lnk) otomatis mendeteksi file executable `.exe` aslinya agar tracking proses berjalan 100% akurat.
  - Status: **SELESAI (0 Errors, 0 Warnings)**.

### Fase 6: Integrasi Sistem Desktop
- [x] **Langkah 12: Penempelan ke Wallpaper Desktop (`WorkerW` Parenting)**
  - Layanan modular `DesktopManagerService.cs`: mengimplementasikan penempelan layer desktop native Windows 11 ala Stardock Fences (`Progman` message `0x052C` & `WorkerW` discovery via `EnumWindows`).
  - Ketahanan terhadap `Win + D` (Show Desktop): saat window ditempelkan ke `WorkerW`, Fence menjadi bagian integral dari desktop layer dan tidak akan pernah ter-minimize saat pengguna menampilkan desktop.
  - Perilaku Z-Order alami: aplikasi lain (browser, game, word) otomatis berada di atas Fence, sedangkan Fence tetap berada di atas wallpaper desktop tepat di belakang window aktif.
  - Kebersihan Taskbar & Alt+Tab: `ShowInTaskbar="False"` dan gaya child window memastikan AeroFence tidak mengotori taskbar maupun menu pergantian jendela (Alt + Tab).
  - Mekanisme drag window native: implementasi `StartWindowDrag` via `ReleaseCapture` dan `WM_NCLBUTTONDOWN` (`HTCAPTION`), memungkinkan pergeseran jendela secara bebas tanpa batasan WPF `DragMove`.
  - Persistensi posisi & ukuran: koordinat `X, Y` serta dimensi `Width, Height` otomatis tersimpan ke `fences.json` setiap kali jendela digeser atau di-resize.
  - Toggle dinamis: menu pengaturan (`⚙️`) menyediakan opsi `"Pin to Desktop (Wallpaper layer)"` untuk berpindah fleksibel antara mode floating window dan mode desktop wallpaper.
  - Status: **SELESAI (0 Errors, 0 Warnings)**.
