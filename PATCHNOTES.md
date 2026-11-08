# Patch Notes

## v1.1.0 — 9 November 2026

Update besar untuk UI, tray, lirik, shelf, dan penataan window.

### Fitur baru

- **Fullscreen Auto-Hide** — Island otomatis sembunyi saat game atau video layar penuh (F11, VLC, YouTube) aktif di monitor yang sama, lalu muncul kembali saat keluar fullscreen.
- **Global Hotkeys** — Shortcut keyboard global:
  - `Win + Alt + N` — toggle island
  - `Win + Alt + P` — buka pemutar
  - `Win + Alt + S` — buka shelf
  - `Win + Alt + L` — buka lirik
  - `Win + Alt + F` — buka fokus
- **Single-Instance Wake** — Menjalankan ulang aplikasi langsung membangunkan island yang sudah aktif, bukan membuka instance baru.
- **Lirik otomatis saat memutar** — Saat musik mulai diputar dan lirik tersedia, island otomatis beralih ke tampilan lirik (bisa dimatikan dari tray).
- **Lock lirik** — Kunci tampilan lirik agar tidak berpindah otomatis.
- **Shelf persisten** — Item Shelf (file/folder) disimpan di `settings.json` dan dipulihkan otomatis setelah restart. Path yang sudah tidak valid otomatis dibersihkan.
- **Toggles di tray menu** — Menu tray baru:
  - Lirik otomatis saat memutar musik
  - Sembunyikan saat layar penuh
  - Shortcut global (Win+Alt+N)

### Perbaikan

- **Posisi media lebih akurat** — Progress bar memperhitungkan waktu sejak `LastUpdatedTime` dari Windows SMTC, sehingga posisi lagu tidak melompat saat seek/pause/resume.
- **Autostart lebih aman** — Pembacaan dan penulisan Run key dibungkus error handling; tidak lagi gagal diam-diam jika registry tidak dapat diakses.
- **Deteksi fullscreen presisi** — Hanya menyembunyikan island jika jendela foreground menempati monitor yang sama dan benar-benar menutupi layar; jendela shell (desktop, taskbar) diabaikan.
- **Pesan lirik lebih natural** — Copy status lirik diperhalus (mis. "Liriknya belum ketemu", "Belum ada lagu yang diputar").
- **Teks idle hidup** — Idle view kini menampilkan lirik baris saat ini atau judul lagu yang sedang berjalan, bukan hanya indikator statis.

### UI / Visual

- **Palet warna baru** — Gradient ungu-lavender untuk aksen (indigo, violet, amber, rose, coral), background lebih hangat (`#06030F`).
- **Tombol interaktif** — Semua tombol utama kini punya animasi hover scale halus dan sudut asimetris (`11,9` / `12,10` / `13,11`).
- **Play button** — Glow ungu (`#8B5CF6`) dan animasi hover.
- **Progress & slider** — Warna track baru (`#2EE9DEFF`) dan thumb senada tema.
- **Typografi** — Line height eksplisit pada Base/Title/Big/Caption/Tiny text untuk rendering lebih konsisten.
- **Lyrics view ringkas** — Layout lebih paduk (360×108), artwork menyusut dengan bayangan ungu, layout adaptif untuk mode compact saat browser di foreground.
- **Idle view baru** — Ticker lirik di tengah, hint "Tahan Alt untuk membuka", indikator EQ saat memutar, dan status charging.
- **Skala konten adaptif** — Konten diskalakan 0.86× saat browser aktif agar island terasa lebih ringkas.

### Developer

- **`package.json`** — Skrip npm: `start`, `dev` (watch), `build`, `publish`, `clean`, dll.
- **`tools/dev-watch.ps1`** — Dev watcher: build ulang otomatis saat file berubah dan restart aplikasi.
- **README** — Fitur dan roadmap diperbarui (Shelf persisten, fullscreen auto-hide, global hotkeys, single-instance wake sudah dicentang).

---

## v1.0.0 — Rilis awal

- Dynamic island di atas layar Windows (WPF + .NET 8).
- Media session (SMTC): play/pause, next/prev, progress, volume.
- Lirik tersinkron dari LRCLIB dengan offset manual.
- Artwork fallback: media session → Deezer → iTunes.
- Pomodoro focus, target air harian, shelf drag & drop, peek notification.
- Tray menu, autostart, dan pengaturan di `%APPDATA%\NusaLite\settings.json`.
