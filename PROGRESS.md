# PROGRESS — NusaLite

> File ini adalah SATU-SATUNYA sumber progres. Update di akhir setiap sesi (maks. 5 menit).
> Aturan: bagian "Status saat ini" selalu ditimpa dengan yang terbaru (jangan menumpuk).
> Log harian: maks. 5 baris per hari, tanpa paste kode. Detail teknis masuk ke commit message / issue, bukan di sini.

## Status saat ini
- Sedang dikerjakan: Sesi 20 — perbaikan regresi tinggi panel Lyrics compact yang terpotong sejak Sesi 19.
- Terakhir selesai: akar masalah ditemukan—padding akar Lyrics 16 memakan 12 px dari tinggi tetap compact 76 sehingga baris lirik hanya tersisa 11 px; margin akar compact kini 8 (full tetap 16), tinggi full tetap 142 dan compact tetap 76; build Release 0 error/6 warning lama CS0169/CS0067.
- Langkah berikutnya persis: user verifikasi visual full Lyrics 142 dan compact 76 (termasuk saat browser foreground); bila lolos, lanjut commit.
- Blocker / keputusan penting: perubahan P2 lain (font, spacing, MorphEase, EqBars, radius) tidak diubah; artwork header/P3 tidak disentuh. Agen tidak menjalankan aplikasi atau berinteraksi desktop.

## Roadmap (dari README)
- [ ] Persistensi item Shelf setelah restart
- [ ] Dukungan multi-monitor dan DPI campuran
- [ ] Pilihan tema dan aksen
- [ ] Pengaturan durasi Pomodoro dan target air dari UI
- [ ] Dukungan source lirik tambahan
- [ ] Installer dan build release otomatis

## Log harian
### 2026-11-13
- Sesi 17: tambah token EdgeHi/EdgeLo/IslandEdgeBrush (putih 0.16→0 di 6% atas, hitam 0.35 di 3% bawah; Terang dibalik: putih 0.5 / hitam 0.08), PanelGlowColor/Brush (#7C5CFF) dan AccentShadowColor; lapisan tepi + glow dipasang di dalam IslandHost tanpa mengubah SetWindowRgn.
- Glow panel aktif hanya untuk Expanded/Lyrics/Focus (Idle & Peek tanpa glow); bayangan PlayButton dan tombol Pause Fokus disamakan (opacity 0.40, blur 14, depth 4, arah bawah).
- Build Release 0 error/0 warning; instance lama dihentikan hanya untuk melepas lock exe; visual belum diverifikasi agen.
- Sesi 17b–18: PanelGlow 0.22 → 0.15 lalu token PanelGlowOpacity per tema (Graphite/Void 0.0, Light 0.15) via DynamicResource; glow mati di tema gelap; build Release 0 error.
- Sesi 19: font 6 view ke tangga resmi (Focus timer 32 Light; lirik non-aktif 13; Hub clock 18); margin/padding kelipatan 4 dan radius panel 24/kartu 16/artwork 12-8; EqBars Idle 4×2 px, 4–14 px, 0.8/0.9/1.1/1.3 dtk; MorphEase bersama untuk morph 300–340 ms + crossfade konten mulai bersamaan; build Release 0 error.
- Sesi 20: regresi Lyrics compact—padding akar 16 menyisakan 11 px untuk baris lirik di tinggi tetap 76; margin akar compact 8 (full tetap 16) dan baris aktif full tanpa margin; full tetap 142/compact 76; build Release 0 error.

### 2026-11-12
- Sesi 15–16: perbaikan gaya A (4 fase + return halus) lalu perbaikan klip: Grid langsung membuat layout slot teks 93 px walau ActualWidth selebar konten, sehingga layout clip memotong glif >93 (probe: ink 0..92; Slide -54 hanya sampai x=38). `Line` dibungkus StackPanel horizontal → DesiredSize/ActualWidth selebar konten, clip hanya di Host (probe: Slide -54 mencapai x=90), tanpa trimming; build Release 0 error. MarqueeDiag dan seluruh log sudah dicabut sebelumnya.
- Sesi 14: IdleLine mode judul untuk lagu baru (5 dtk + lirik pertama aktif) lewat tick ApplyMedia; IdleLine PropertyChanged sudah memicu BuildIdleSpec/UpdateIdleLayout (lebar pill ikut); IdleView/LyricTicker tidak diubah; build Release 0 error.
- Sesi 12–13: ganti strategi marquee—ScrollingText baru (TextBlock+clip) lalu ditulis ulang berbasis Stopwatch + CompositionTarget.Rendering, offset = elapsed mod (1,2 dtk + overflow/35 + 1,2 dtk parkir), tanpa storyboard/timer baru; Loaded hanya attach, re-parent/reload tidak reset origin, hanya perubahan teks yang reset; dipakai untuk DateText + TrackArtist Hub; uji ukur: "Nov" 73,05–93, "memulai" 105,15–147, siklus 3,527/3,198 dtk, simulasi Unloaded+Loaded resetDetected=False; build Release 0 error.
- Sesi 11: MarqueeText disegarkan layout-nya sebelum mengukur, overflow = lebar teks aktual − lebar Host ber-ClipToBounds, offset akhir -overflow, durasi overflow/35, restart saat ukuran Line berubah; ujung kalimat kini tampil saat jeda akhir; build Release 0 error.
- Sesi 10: MarqueeText baru (TextBlock+clip, animasi 1,2 dtk/35 px per dtk/1,2 dtk, reset saat teks berubah) dipakai di TitleLine+Prev/Current/Next LyricsView, DateText dan TrackArtist Hub; elipsis/MaxWidth lama dilepas; LyricTicker idle tak berubah; build Release 0 error.
- Sesi 9: token FrameLineColor/FrameLineBrush (Graphite/Void #1AFFFFFF, Light #8C6A5AE0); Border akar SettingsWindow pakai FrameLineBrush; mode gelap tak berubah; build Release 0 error.
- Sesi 8: SettingsWindow jadi chrome custom rounded (WindowStyle None/AllowsTransparency, Border 16 + shadow ShadowColor, title bar ikon/judul/min/close, drag DragMove); ukuran 664x484 agar konten 640x460; logika settings utuh; build Release 0 error.

### 2026-11-11
- Sesi 3/3b: token semantik + sapu hex XAML ke DynamicResource; token FillGhost10/FillFaint12/LineSoft1F/TrackSoft20/TextGhost59/TextMuted66/AccentTint12/18/1E/2E/30/38/40/AccentLine5C/AccentFill7F/MintTint1F/33/DangerSurface ditambahkan di tiga tema; AccentBrush Graphite/Light gradient sendiri; build 0 error.
- Sesi 4/Hotfix: pemilih tema segmented Graphite/Void/Terang di Umum; klik langsung ThemeService.Apply + simpan AppSettings.Theme; reset ke bawaan menerapkan Graphite; LyricTicker internal TextBlock Foreground StaticResource -> DynamicResource InkBrush; build Release 0 error (aplikasi ditutup sementara).
- Sesi 5: shadow island ShadowColor 18/3/0.35 + stroke LineBrush 1 + TextFormattingMode Display; ShelfCard 13->16, drop overlay 14->16; EqBars AccentBrush; audit warna static XAML beres; build Release 0 error; shadow berpotensi terpotong region window.
- Sesi 6: ThemeService.ThemeChanged + dot idle resolve ulang/UpdateDots; token DotIdleColor/Brush di tiga tema; ContextMenu Shelf pakai Panel/Line/Ink/FillMedium saat dibuat; build Release 0 error.
- Sesi 7: Theme.Light DotsBar #99FFFFFF + DotIdle #8C000000; LyricsView header Title/Artist pindah ke kolom lebar, artwork-only 56; tinggi normal 108->142, compact 76 tetap; build Release 0 error.

### 2026-11-10
- Selesai ikon sumber media di Idle (Spotify/YouTube Music/YouTube/browser/pemutar lain) + lirik 13.5; uji visual lolos; build Release 0 error; commit & push.
- Pecah MainWindow tuntas: Hotkeys, Fullscreen, DragDrop, Peek & Dots, Guard, Pointer, Animation; MainWindow.xaml.cs tinggal inti (~481 baris), build Release 0 error.
- Fondasi tema: Colors/Brush dipindah ke Themes/Theme.Graphite.xaml (nilai sama), ThemeService + AppSettings.Theme (default Graphite), App.xaml merge tema dulu; brush di Views/Styles jadi DynamicResource; build 0 error, tampilan identik.
- Palet tema: Void (lama), Graphite (netral, tanpa Lime, gradien 2 titik), Light (surface terang/teks gelap); ThemeService Void/Graphite/Light; Notch Fill ikut VoidBrush (1 baris, disetujui); build 0 error; cek Light island terbaca, settings.json dikembalikan ke Graphite.

### 2026-11-09
- Push update UI, tray, lyrics, shelf, dan window styling, plus patch notes v1.1.0.
- Setup AGENTS.md + PROGRESS.md untuk hemat token dan simpan progres harian di OpenCode.
- Tambah hover dwell delay: AppSettings.HoverDelayMs (default 400), timer jeda di MainWindow agar kursor yang sekadar lewat tidak langsung meng-expand island; HoverDelayMs=0 = perilaku lama.
- Tambah mode Click Guard: AppSettings.ClickGuardEnabled (default false), item tray "Click Guard (tembus klik)", hotkey Win+Alt+G (id 9006); saat aktif island tembus klik dan tidak expand oleh hover, peek tetap tampil.
- Tambah Auto Click Guard: AppSettings.AutoClickGuard (default false); guard otomatis aktif saat jendela foreground aplikasi lain maximized (cek WS_MAXIMIZE via GetWindowLongPtr) dan lepas sendiri. Digabung lewat IsEffectiveGuard (manual || auto) yang dibaca UpdateClickThrough + jalur hover.
- Dokumentasi v1.2.0: README (fitur Hover Dwell/Click Guard/Auto Click Guard + hotkey Win+Alt+G), PATCHNOTES entri baru, catat setting HoverDelayMs, ClickGuardEnabled, AutoClickGuard.
- Tambah Guard Ghost: AppSettings.GuardOpacity (default 0.5, clamp 0.15..1.0); saat Click Guard aktif jendela tembus pandang, AnimateWindowOpacity(250ms) + _ghostTimer(1400ms) dengan BumpGhost() memudar jelas (0.95) saat LyricCurrent/IdleLine berubah (view Idle) atau peek muncul. UpdateClickThrough & XAML tidak diubah.
- Revisi Guard Ghost jadi berbasis kursor: helper IsCursorNearIsland() (margin 24px, reuse IsInside), state _ghostFaded dikelola di HoverTimer_Tick (tanpa timer baru); kursor dekat -> pudar ke GuardOpacity, kursor jauh -> pulih 0.95 setelah _ghostTimer 350ms. Pemicu lirik/Idle dihapus; peek tetap 0.95 lalu pulih sesuai kursor.
- Bugfix: selama Click Guard aktif island tidak lagi pindah ke Lyrics besar saat lagu baru. Penegakan di SwitchTo (tolak view non-Idle/non-Peek tanpa explicitRequest) + di OnViewModelPropertyChanged (paksa Idle saat guard); parameter explicitRequest=true hanya dari OpenView/OnViewRequested (hotkey/tray). _viewBeforeGuard tidak ditimpa bila guard sudah aktif.
- Keterbacaan: IdleView LyricTicker FontSize 10.5->12, Height 15->17, FontWeight Medium, Foreground InkBrush, MaxWidth 288->320; BuildIdleSpec ukur 12/Medium, tinggi 34->38, clamp 198..360 -> 216..400, radius 12->19.3. LyricsView TitleLine 9.5->10.5 & ArtistLine 8.5->10; HubView LyricTicker 9->10.5, Height 12->14. Baris lirik utama tidak diubah.
- Font UI diganti ke Inter (open source, OFL): Fonts/Inter-Regular/Medium/SemiBold.ttf + OFL.txt didaftarkan sebagai Resource di csproj; UiFont & DisplayFont di Styles.xaml kini "./Fonts/#Inter" dengan fallback SF Pro -> Segoe UI. Catatan: font kini dibundel => ukuran installer/publish naik beberapa ratus KB per bobot font (~1,2 MB total untuk 3 bobot).
- Siapkan rilis v1.2.0: sinkron versi 1.2.0 (csproj + package.json), tambah .github/workflows/release.yml (publish single-file exe + buat GitHub Release saat tag v* dipush), entri v1.2.0 di PATCHNOTES.md + catatan SmartScreen, baris Unduh + tautan PATCHNOTES di README.md. Commit "release: v1.2.0" dan tag v1.2.0 dipush ke main (tidak ada kode aplikasi yang diubah).
- Kerangka jendela Pengaturan: SettingsWindow.xaml/.cs (sidebar Umum, Notch & Guard, Fokus & Air, Tentang + panel konten via Visibility), tray "Pengaturan..." + SettingsRequested, AppHost lazy single-instance window.
- Isi SettingsWindow bagian Umum & Notch/Guard: kontrol terhubung SettingsStore + callback dari AppHost (checkbox autostart/hotkey/fullscreen/auto-lyrik/click guard/auto click guard, offset lirik tervalidasi, slider HoverDelayMs & GuardOpacity); MainWindow.RefreshGuardGhost() untuk perubahan ghost live.
- Selesaikan SettingsWindow: Fokus & Air (durasi fokus/jeda, target air kelipatan 50, jeda pengingat) dengan validasi + revert; Tentang (versi assembly, buka settings.json, tautan Releases, reset ke bawaan + konfirmasi); SettingsStore.ResetToDefaults(); README baris fitur Pengaturan. PATCHNOTES menunggu nomor versi.
- Naikkan versi ke 1.3.0 (NusaLite.csproj, package.json, fallback release.yml) dan push commit fitur Pengaturan ke GitHub; PATCHNOTES v1.3.0 & tag rilis belum dibuat (menunggu instruksi).
- Tambah entri PATCHNOTES v1.3.0 (jendela Pengaturan + catatan perilaku), commit, lalu push tag v1.3.0 untuk memicu GitHub Release otomatis.
