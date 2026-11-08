# NusaLite — Konteks Project (baca ini saja, jangan scan seluruh repo)

## Ringkasan
NusaLite adalah dynamic island / living notch untuk Windows.
Native WPF + .NET 8 (net8.0-windows10.0.19041.0), C#, Nullable enable, ImplicitUsings enable.
Fitur: media session (Windows SMTC), lirik tersinkron (LRCLIB), artwork (fallback Deezer -> iTunes),
kontrol volume (Core Audio), Pomodoro/Fokus, pengingat Air, Shelf drag-file, Peek baterai/volume/media,
tray menu (WinForms NotifyIcon) + autostart via Windows Run key.
Settings lokal: System.Text.Json di %APPDATA%\NusaLite\settings.json. Tanpa database, tanpa Electron.

## Struktur (cukup tahu ini, tidak perlu membaca semua file)
- AppHost.cs — komposisi service, tray, view model, window
- MainWindow.xaml / .xaml.cs — overlay transparan + animasi island (file BESAR: ~800 baris di .cs)
- Views/ — Hub, Player, Lyrics, Focus, Water, Shelf, Peek, Idle
- ViewModels/MainViewModel.cs — state + orkestrasi (file TERBESAR: ~835 baris, baca per-bagian saja)
- Services/ — Media (~570 baris), Lyrics, Artwork (~380 baris), Audio, Power, Pomodoro, Water, Shelf, Tray, SettingsStore
- Controls/ — NotchShape, ArcRing, EqBars, LyricTicker
- Interop/ — Win32 (NativeMethods), CoreAudio
- Infrastructure/ — decoder, converter, logging, observable base
- Themes/Styles.xaml — style & resource visual

## Build
- Build: dotnet build -c Release
- Run: dotnet run -c Release
- Publish: dotnet publish NusaLite.csproj -c Release -r win-x64 --self-contained false

## Aturan kerja (penting untuk hemat token)
1. Satu sesi = satu tugas kecil yang jelas selesai. Jangan campur fitur lain.
2. Sebut file spesifik dengan @file. Jangan minta membaca seluruh repo.
3. Untuk file besar (MainViewModel.cs, MainWindow.xaml.cs, MediaService.cs):
   cari nama method/property dulu, baca hanya bagian itu, jangan baca file utuh.
4. Kalau ada error build: tempel hanya baris error yang relevan, bukan seluruh log.
5. Sebelum mengubah kode besar: buat rencana singkat dulu, tunggu persetujuan, baru eksekusi.
6. Di akhir sesi WAJIB update PROGRESS.md (bagian "Status saat ini" + log hari ini, ringkas, tanpa paste kode).
7. Jangan ubah file di luar tugas tanpa izin. Jangan refactor diam-diam.

## Konvensi
- Branch fitur: feat/nama-fitur. Commit: "feat: ...", "fix: ...", dst.
- Bahasa UI & README: Bahasa Indonesia. Komentar kode: seperlunya, singkat.
