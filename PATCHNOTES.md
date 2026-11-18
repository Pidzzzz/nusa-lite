# Patch Notes

## v1.4.0 — 18 November 2026

Pembaruan tampilan besar: tiga tema, chip Fokus di tiga tampilan, komposisi Fokus baru, tray yang menyatu dengan tema, navigasi chevron, plus perbaikan lirik dan transisi — ditutup dengan fitur cek pembaruan.

> **Catatan pemasangan** — File `.exe` belum ditandatangani secara digital. Saat pertama dijalankan, Windows SmartScreen mungkin menampilkan peringatan; klik **More info** lalu **Run anyway** untuk melanjutkan.

### Fitur baru

- **Tema Graphite, Void, dan Terang** — Pilihan tema di jendela Pengaturan dengan token warna semantik, aksen gradien per tema, dan bobot tipografi yang dikalibrasi; pilihan tersimpan otomatis.
- **Chip Timer Fokus di tiga tampilan** — Ring kecil, label fase, dan waktu tabular muncul di Idle, Lirik, dan Pemutar selama sesi Pomodoro berjalan; klik chip langsung membuka tampilan Fokus.
- **Tampilan Fokus "Cincin Panggung"** — Cincin besar dengan timer di tengah, label fase, jam selesai, tombol Mulai/Jeda/Lanjut, serta Reset dan Lewati.
- **Chevron navigasi di bar dot** — Tombol `‹` dan `›` mengapit deret dot (target 24×24 px, tooltip), memutar urutan Pemutar → Lirik → Fokus → Air → Shelf; klik dot langsung tetap berfungsi.
- **Cek pembaruan** — Item tray "Periksa Pembaruan" dan pemeriksaan otomatis sekali di latar belakang beberapa detik setelah start; bila ada versi lebih baru, notifikasi tray membuka halaman rilis di browser.
- **Menu tray mengikuti tema** — Latar, teks, hover, checkbox, pemisah, dan sorotan pilihan ikut tema aktif (sorotan ungu per tema, bukan biru bawaan Windows).
- **Ikon sumber media di tampilan Idle** — Ikon kecil di samping lirik menunjukkan aplikasi yang sedang memutar media (Spotify, YouTube Music, YouTube, browser, atau pemutar lain); lebar notch menyesuaikan otomatis.

### Perbaikan

- **Lirik lama tidak lagi tertinggal** — Saat lirik tidak ditemukan atau gagal dimuat, baris lama langsung dibersihkan di Lirik, Pemutar, Hub, dan Idle.
- **Lirik Idle lebih rapi saat Pomodoro** — Baris lirik menghormati lebar chip Fokus dan tepi kanan ticker memudar halus hanya selama sesi aktif.
- **Transisi tampilan lebih andal** — Area jendela mengikuti layout terbaru setelah perubahan ukuran, dan konten island selalu disinkronkan dengan tampilan aktif meski animasi transisi terputus, sehingga island yang menyusut ke Idle tidak lagi menampilkan sisa tampilan Pemutar.
- **Timer Fokus terbaca di tema gelap** — Angka timer mengikuti warna teks tema, tidak lagi hitam.
- **Lirik mode compact tidak terpotong** — Padding baris dan margin akar dikembalikan agar tinggi tetap 76 px tanpa memotong baris aktif.
- **Sorotan tray tidak lagi biru** — Warna seleksi/pressed/checkbox diganti palette ungu yang konsisten per tema.

### Catatan perilaku

- **Pemeriksaan pembaruan tidak mengunduh apa pun** — Aplikasi hanya memberi tahu dan membuka halaman rilis; unduh dan pasang tetap manual. Saat offline atau gagal, pemeriksaan otomatis diam tanpa pesan; pemeriksaan manual saat versi sudah terbaru menampilkan "Sudah versi terbaru".

---

## v1.3.0 — 9 November 2026

Jendela **Pengaturan** baru untuk mengatur NusaLite tanpa harus mengedit `settings.json`.

> **Catatan pemasangan** — File `.exe` belum ditandatangani secara digital. Saat pertama dijalankan, Windows SmartScreen mungkin menampilkan peringatan; klik **More info** lalu **Run anyway** untuk melanjutkan.

### Fitur baru

- **Jendela Pengaturan** — Buka dari tray menu ("Pengaturan..."), dengan sidebar empat bagian:
  - **Umum** — Mulai bersama Windows, shortcut global, sembunyikan saat layar penuh, lirik otomatis, dan offset lirik (`-2000..2000` ms).
  - **Notch & Guard** — Click Guard, Auto Click Guard, jeda hover (`0..1000` ms, langkah 50), dan opasitas Guard Ghost (`15..100%`).
  - **Fokus & Air** — Durasi fokus (`1..180` menit), durasi jeda (`1..60` menit), target air harian (`500..5000` ml, kelipatan 50), dan jeda pengingat air (`5..240` menit).
  - **Tentang** — Versi aplikasi, tombol buka `settings.json`, tautan halaman rilis, dan tombol **Kembalikan ke bawaan** (dengan konfirmasi).
- **Penerapan langsung** — Perubahan toggle langsung berlaku seperti lewat tray menu; nilai slider dan kolom angka tersimpan otomatis saat valid.

### Catatan perilaku

- **Durasi fokus/jeda Pomodoro** — Nilai baru berlaku pada sesi berikutnya (setelah Reset/Skip atau ganti fase); sesi yang sedang berjalan tidak berubah.
- **Target air dan pengingat** — Langsung dipakai setelah diubah.
- **Reset ke bawaan** — Mengembalikan semua pengaturan ke nilai awal, termasuk mematikan autostart bila sebelumnya aktif.

---

## v1.2.0 — 9 November 2026

Kontrol hover dan klik yang lebih tenang, plus pembaruan tampilan dan font.

> **Catatan pemasangan** — File `.exe` belum ditandatangani secara digital. Saat pertama dijalankan, Windows SmartScreen mungkin menampilkan peringatan; klik **More info** lalu **Run anyway** untuk melanjutkan.

### Fitur baru

- **Hover Dwell** — Notch tidak lagi langsung membesar saat kursor hanya melintas; kini menunggu jeda niat singkat (`HoverDelayMs`, default 400 ms) sebelum membuka. Bila diatur ke `0`, perilakunya kembali seperti semula.
- **Click Guard** — Mode tembus klik total. Saat aktif, notch tidak menangkap klik sama sekali dan tidak membesar oleh hover. Diaktifkan dari tray menu ("Click Guard (tembus klik)") atau `Win + Alt + G`. Peek tetap boleh tampil.
- **Auto Click Guard** — Bila diaktifkan, Click Guard menyala otomatis selama jendela aplikasi lain sedang dalam keadaan maximized, lalu lepas sendiri begitu kondisi berakhir. Tidak aktif terhadap manual guard, notch sendiri, atau desktop.
- **Guard Ghost** — Selama Click Guard aktif, notch dibuat tembus pandang. Notch memudar lebih jelas saat kursor mendekat, dan kembali tembus pandang ketika kursor menjauh.
- **Hotkey baru** — `Win + Alt + G` untuk toggle Click Guard.

### Perbaikan

- **Click Guard tidak lagi terkunci di lirik besar** — Saat Click Guard aktif, island tidak lagi berpindah ke tampilan Lyrics besar ketika lagu baru mulai diputar; island tetap di Idle kecuali saat Peek muncul sesaat.

### UI / Visual

- **Lirik Idle & teks kecil lebih terbaca** — Ukuran lirik di tampilan Idle, judul/artis di tampilan Lyrics, dan lirik mini di Hub dinaikkan; spesifikasi ukuran island Idle ikut disesuaikan.
- **Font Inter** — Keluarga font UI diganti ke **Inter** (open source, lisensi OFL) yang dibundel bersama aplikasi, dengan fallback aman ke SF Pro lalu Segoe UI.

### Pengaturan baru

- `HoverDelayMs` — jeda hover sebelum notch membuka (default `400`).
- `ClickGuardEnabled` — status Click Guard manual (default `false`).
- `AutoClickGuard` — aktifkan Click Guard otomatis saat ada jendela maximized (default `false`).
- `GuardOpacity` — tingkat transparansi notch saat Click Guard aktif (default `0.5`).

---

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
