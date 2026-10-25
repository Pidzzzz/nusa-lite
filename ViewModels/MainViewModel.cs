using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using NusaLite.Infrastructure;
using NusaLite.Models;
using NusaLite.Services;

namespace NusaLite.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore _settings;
    private readonly MediaService _media;
    private readonly LyricsService _lyrics;
    private readonly Interop.CoreAudio.AudioService _audio;
    private readonly PowerService _power;
    private readonly PomodoroService _pomodoro;
    private readonly WaterService _water;
    private readonly ShelfService _shelf;

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _volumeTimer;
    private readonly DispatcherTimer _powerTimer;

    private TrackInfo _track = TrackInfo.Empty;
    private TrackInfo _lastLyricsTrack = TrackInfo.Empty;
    private IReadOnlyList<LyricLine> _lyricLines = Array.Empty<LyricLine>();
    private LyricsResult _lyricsResult = new() { Status = LyricsStatus.NoMedia };
    private CancellationTokenSource? _lyricsCts;
    private string _lastLyricKey = string.Empty;
    private int _lyricIndex = -1;

    private bool _syncingVolume;
    private DateTime _lastVolumeWrite = DateTime.MinValue;
    private PowerInfo _lastPower = PowerInfo.Unknown;
    private bool _powerSeen;

    private string _clockText = string.Empty;
    private string _dateText = string.Empty;
    private int _volumeValue = 50;
    private bool _isMuted;
    private bool _pinned;
    private string _peekTitle = string.Empty;
    private string _peekSubtitle = string.Empty;
    private string _peekValue = string.Empty;
    private double _peekProgress;
    private ImageSource? _peekArtwork;
    private PeekKind _peekKind;

    private string _lyricPrev = string.Empty;
    private string _lyricCurrent = string.Empty;
    private string _lyricNext = string.Empty;

    private bool _shelfDropHover;

    public MainViewModel(
        SettingsStore settings,
        MediaService media,
        LyricsService lyrics,
        Interop.CoreAudio.AudioService audio,
        PowerService power,
        PomodoroService pomodoro,
        WaterService water,
        ShelfService shelf)
    {
        _settings = settings;
        _media = media;
        _lyrics = lyrics;
        _audio = audio;
        _power = power;
        _pomodoro = pomodoro;
        _water = water;
        _shelf = shelf;

        _pinned = settings.Current.PinOpen;

        _media.Changed += ApplyMedia;
        _pomodoro.Updated += RaisePomodoro;
        _pomodoro.Completed += OnPomodoroCompleted;
        _water.Updated += RaiseWater;
        _water.ReminderDue += OnWaterReminder;
        _shelf.Changed += RaiseShelf;

        UpdateClock();
        _volumeValue = _audio.GetVolumePercent();
        _isMuted = _audio.GetMuted();
        _lastPower = _power.Read();
        _powerSeen = false;

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        _volumeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        _volumeTimer.Tick += (_, _) => PollVolume();
        _volumeTimer.Start();

        _powerTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _powerTimer.Tick += (_, _) => PollPower();
        _powerTimer.Start();

        ApplyMedia();

        if (_track.HasTrack)
        {
            _ = LoadLyricsAsync(_track);
        }
    }

    public event Action<PeekKind>? PeekRequested;

    public event Action<IslandView>? ViewRequested;

    public event Action<bool>? PinRequested;

    public event Action<string, string>? NotifyRequested;

    public ObservableCollection<ShelfItem> ShelfItems => _shelf.Items;

    public MediaService Media => _media;

    public async Task InitializeAsync()
    {
        await _media.InitializeAsync();
        ApplyMedia();
    }

    // ----- Media ---------------------------------------------------------

    public string TrackTitle => string.IsNullOrWhiteSpace(_track.Title) ? "Tidak ada media" : _track.Title;

    public string TrackArtist => _track.HasTrack ? _track.ArtistAlbum : "Putar musik untuk memulai";

    public string TrackSource => _track.HasTrack ? _track.SourceName : "NusaLite";

    public bool HasMedia => _track.HasTrack;

    public bool IsPlaying => _track.IsPlaying;

    public ImageSource? Artwork => _track.Artwork;

    public bool HasArtwork => _track.Artwork is not null;

    public string PositionText => TrackInfo.Format(_track.Position);

    public string DurationText => _track.Duration > TimeSpan.Zero ? TrackInfo.Format(_track.Duration) : "--:--";

    public double TrackProgress => _track.Progress;

    public string PlayGlyph => IsPlaying ? "pause" : "play";

    public void TogglePlay() => _ = _media.TogglePlayAsync();

    public void NextTrack() => _ = _media.NextAsync();

    public void PreviousTrack() => _ = _media.PreviousAsync();

    public void SeekToFraction(double fraction)
    {
        if (_track.Duration <= TimeSpan.Zero)
        {
            return;
        }

        fraction = Math.Clamp(fraction, 0, 1);
        var target = TimeSpan.FromTicks((long)(_track.Duration.Ticks * fraction));
        _ = _media.SeekAsync(target);
        _track = _track with { Position = target };
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(TrackProgress));
    }

    private void ApplyMedia()
    {
        var track = _media.Current;

        if (track.Key != _track.Key)
        {
            var hadTrack = _track.HasTrack;
            _track = track;
            RaiseTrackChanged();

            if (track.HasTrack)
            {
                _ = LoadLyricsAsync(track);
            }
            else
            {
                ClearLyrics(LyricsStatus.NoMedia);
            }

            if (!hadTrack && track.HasTrack)
            {
                PeekRequested?.Invoke(PeekKind.Media);
            }
        }
        else
        {
            var oldPlaying = _track.IsPlaying;
            var artworkChanged = !ReferenceEquals(_track.Artwork, track.Artwork);
            _track = track;
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(PlayGlyph));
            OnPropertyChanged(nameof(PositionText));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(TrackProgress));
            UpdateLyricIndex();

            if (artworkChanged)
            {
                OnPropertyChanged(nameof(Artwork));
                OnPropertyChanged(nameof(HasArtwork));

                if (PeekKind == PeekKind.Media)
                {
                    PeekArtwork = track.Artwork;
                    OnPropertyChanged(nameof(PeekHasArtwork));
                }
            }

            if (!oldPlaying && track.IsPlaying)
            {
                PeekRequested?.Invoke(PeekKind.Media);
            }
        }
    }

    private void RaiseTrackChanged()
    {
        OnPropertyChanged(nameof(TrackTitle));
        OnPropertyChanged(nameof(TrackArtist));
        OnPropertyChanged(nameof(TrackSource));
        OnPropertyChanged(nameof(HasMedia));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(Artwork));
        OnPropertyChanged(nameof(HasArtwork));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(TrackProgress));
        OnPropertyChanged(nameof(PlayGlyph));
    }

    // ----- Lyrics --------------------------------------------------------

    public bool HasLyrics => _lyricLines.Count > 0;

    public string LyricPrev => _lyricPrev;

    public string LyricCurrent => _lyricCurrent;

    public string LyricNext => _lyricNext;

    public string LyricsMessage => _lyricsResult.Message;

    public bool ShowLyricsMessage => _lyricLines.Count == 0 && !string.IsNullOrEmpty(LyricsMessage);

    public string LyricOffsetText
    {
        get
        {
            var value = _settings.Current.LyricOffsetMs / 1000.0;
            return value == 0 ? "Offset 0s" : $"Offset {value:+0.0;-0.0}s";
        }
    }

    private void ClearLyrics(LyricsStatus status)
    {
        _lyricsCts?.Cancel();
        _lyricsCts = null;
        _lyricLines = Array.Empty<LyricLine>();
        _lyricIndex = -1;
        _lyricPrev = string.Empty;
        _lyricCurrent = string.Empty;
        _lyricNext = string.Empty;
        _lastLyricKey = string.Empty;
        _lyricsResult = new LyricsResult { Status = status };
        RaiseLyrics();
    }

    private async Task LoadLyricsAsync(TrackInfo track)
    {
        _lyricsCts?.Cancel();
        var cts = new CancellationTokenSource();
        _lyricsCts = cts;
        _lastLyricsTrack = track;
        _lyricLines = Array.Empty<LyricLine>();
        _lyricIndex = -1;
        _lyricsResult = new LyricsResult { Status = LyricsStatus.Loading };
        RaiseLyrics();

        try
        {
            var result = await _lyrics.GetAsync(track, cts.Token);
            if (cts.IsCancellationRequested || _track.Key != track.Key)
            {
                return;
            }

            _lyricsResult = result;
            _lyricLines = result.Lines;
            _lastLyricKey = string.Empty;
            UpdateLyricIndex();
            RaiseLyrics();
        }
        catch (OperationCanceledException)
        {
            // A new track replaced this lookup.
        }
        catch (Exception ex)
        {
            Log.Write("Loading lyrics failed.", ex);
            if (!cts.IsCancellationRequested)
            {
                _lyricsResult = new LyricsResult { Status = LyricsStatus.Error };
                RaiseLyrics();
            }
        }
    }

    private void UpdateLyricIndex()
    {
        if (_lyricLines.Count == 0)
        {
            return;
        }

        var index = LyricsService.FindLine(_lyricLines, _track.Position, _settings.Current.LyricOffsetMs);
        if (index < 0)
        {
            index = 0;
        }

        var prev = index > 0 ? _lyricLines[index - 1].Text : string.Empty;
        var current = _lyricLines[index].Text;
        var next = index + 1 < _lyricLines.Count ? _lyricLines[index + 1].Text : string.Empty;
        var key = $"{index}\u001f{prev}\u001f{current}\u001f{next}";

        if (key == _lastLyricKey)
        {
            return;
        }

        _lastLyricKey = key;
        _lyricIndex = index;
        _lyricPrev = prev;
        _lyricCurrent = current;
        _lyricNext = next;
        OnPropertyChanged(nameof(LyricPrev));
        OnPropertyChanged(nameof(LyricCurrent));
        OnPropertyChanged(nameof(LyricNext));
    }

    private void RaiseLyrics()
    {
        OnPropertyChanged(nameof(HasLyrics));
        OnPropertyChanged(nameof(LyricPrev));
        OnPropertyChanged(nameof(LyricCurrent));
        OnPropertyChanged(nameof(LyricNext));
        OnPropertyChanged(nameof(LyricsMessage));
        OnPropertyChanged(nameof(ShowLyricsMessage));
        OnPropertyChanged(nameof(LyricOffsetText));
    }

    public void ShiftLyricOffset(int deltaMs)
    {
        _settings.Current.LyricOffsetMs = Math.Clamp(_settings.Current.LyricOffsetMs + deltaMs, -10000, 10000);
        _settings.Save();
        _lastLyricKey = string.Empty;
        UpdateLyricIndex();
        OnPropertyChanged(nameof(LyricOffsetText));
    }

    // ----- Clock ---------------------------------------------------------

    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public string DateText
    {
        get => _dateText;
        private set => SetProperty(ref _dateText, value);
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText = now.ToString("HH:mm");
        DateText = now.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
    }

    // ----- Volume & power ------------------------------------------------

    public int VolumeValue
    {
        get => _volumeValue;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (!SetProperty(ref _volumeValue, clamped))
            {
                return;
            }

            OnPropertyChanged(nameof(VolumeText));
            if (_syncingVolume)
            {
                return;
            }

            _audio.SetVolumePercent(clamped);
            _isMuted = false;
            OnPropertyChanged(nameof(IsMuted));
            _lastVolumeWrite = DateTime.Now;
            PeekRequested?.Invoke(PeekKind.Volume);
        }
    }

    public string VolumeText => _isMuted ? "Bisu" : $"{VolumeValue}%";

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(VolumeText));
            }
        }
    }

    public void ToggleMute()
    {
        _audio.SetMuted(!_isMuted);
        IsMuted = !_isMuted;
        _lastVolumeWrite = DateTime.Now;
        PeekRequested?.Invoke(PeekKind.Volume);
    }

    private void PollVolume()
    {
        try
        {
            if ((DateTime.Now - _lastVolumeWrite).TotalMilliseconds < 900)
            {
                return;
            }

            var volume = _audio.GetVolumePercent();
            var muted = _audio.GetMuted();
            var changed = volume != _volumeValue || muted != _isMuted;

            _syncingVolume = true;
            _volumeValue = volume;
            OnPropertyChanged(nameof(VolumeValue));
            OnPropertyChanged(nameof(VolumeText));
            IsMuted = muted;
            _syncingVolume = false;

            if (changed)
            {
                PeekRequested?.Invoke(PeekKind.Volume);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Volume poll failed.", ex);
        }
    }

    public bool HasBattery => _lastPower.Present;

    public int BatteryPercent => _lastPower.Percent < 0 ? 0 : _lastPower.Percent;

    public string BatteryText => _lastPower.PercentText;

    public bool IsCharging => _lastPower.IsPlugged;

    public double BatteryProgress => _lastPower.Percent <= 0 ? 0 : _lastPower.Percent / 100.0;

    public string BatteryStateText => _lastPower.IsPlugged ? "Mengisi daya" : "Baterai";

    public string PowerPeekSubtitle => _lastPower.IsPlugged
        ? (_lastPower.Percent >= 98 ? "Baterai penuh" : "Sedang mengisi daya")
        : "Menggunakan baterai";

    private void PollPower()
    {
        try
        {
            var power = _power.Read();
            if (power == _lastPower)
            {
                return;
            }

            var wasPlugged = _lastPower.IsPlugged;
            _lastPower = power;
            OnPropertyChanged(nameof(HasBattery));
            OnPropertyChanged(nameof(BatteryPercent));
            OnPropertyChanged(nameof(BatteryText));
            OnPropertyChanged(nameof(IsCharging));
            OnPropertyChanged(nameof(BatteryProgress));
            OnPropertyChanged(nameof(BatteryStateText));
            OnPropertyChanged(nameof(PowerPeekSubtitle));

            if (_powerSeen && wasPlugged != power.IsPlugged)
            {
                PeekRequested?.Invoke(PeekKind.Power);
            }

            _powerSeen = true;
        }
        catch (Exception ex)
        {
            Log.Write("Power poll failed.", ex);
        }
    }

    // ----- Pomodoro ------------------------------------------------------

    public string PomodoroTime => _pomodoro.TimeText;

    public string PomodoroPhaseLabel => _pomodoro.PhaseLabel;

    public double PomodoroProgress => _pomodoro.Progress;

    public string PomodoroSessions => _pomodoro.SessionsText;

    public string PomodoroStartLabel => _pomodoro.StartLabel;

    public bool PomodoroRunning => _pomodoro.IsRunning;

    public string PomodoroDuration => _pomodoro.DurationText;

    public bool PomodoroIsBreak => _pomodoro.Phase == PomodoroPhase.Break;

    public void TogglePomodoro() => _pomodoro.Toggle();

    public void ResetPomodoro() => _pomodoro.Reset();

    public void SkipPomodoro() => _pomodoro.Skip();

    private void RaisePomodoro()
    {
        OnPropertyChanged(nameof(PomodoroTime));
        OnPropertyChanged(nameof(PomodoroPhaseLabel));
        OnPropertyChanged(nameof(PomodoroProgress));
        OnPropertyChanged(nameof(PomodoroSessions));
        OnPropertyChanged(nameof(PomodoroStartLabel));
        OnPropertyChanged(nameof(PomodoroRunning));
        OnPropertyChanged(nameof(PomodoroDuration));
        OnPropertyChanged(nameof(PomodoroIsBreak));
    }

    private void OnPomodoroCompleted(PomodoroPhase completed)
    {
        var title = completed == PomodoroPhase.Focus ? "Fokus selesai" : "Istirahat selesai";
        var message = completed == PomodoroPhase.Focus
            ? "Waktunya istirahat sejenak."
            : "Siap untuk sesi fokus berikutnya.";
        NotifyRequested?.Invoke(title, message);
    }

    // ----- Water ---------------------------------------------------------

    public int WaterToday => _water.TodayMl;

    public string WaterAmount => _water.AmountText;

    public string WaterPercent => _water.PercentText;

    public double WaterProgress => _water.Progress;

    public string WaterRemaining => _water.RemainingText;

    public bool WaterGoalReached => _water.GoalReached;

    public IReadOnlyList<WaterDay> WaterDays => _water.LastWeek;

    public string WaterReminderText
    {
        get
        {
            var minutes = _settings.Current.WaterReminderMinutes;
            return $"Pengingat tiap {minutes} menit";
        }
    }

    public void AddWater(int milliliters = 250) => _water.Add(milliliters);

    public void ResetWater() => _water.ResetToday();

    private void RaiseWater()
    {
        OnPropertyChanged(nameof(WaterToday));
        OnPropertyChanged(nameof(WaterAmount));
        OnPropertyChanged(nameof(WaterPercent));
        OnPropertyChanged(nameof(WaterProgress));
        OnPropertyChanged(nameof(WaterRemaining));
        OnPropertyChanged(nameof(WaterGoalReached));
        OnPropertyChanged(nameof(WaterDays));
        OnPropertyChanged(nameof(WaterReminderText));
    }

    private void OnWaterReminder()
        => NotifyRequested?.Invoke("Waktunya minum", $"Sudah {_water.TodayMl} ml hari ini. {_water.RemainingText}.");

    // ----- Shelf ---------------------------------------------------------

    public bool HasShelfItems => _shelf.Items.Count > 0;

    public string ShelfCountText
    {
        get
        {
            var count = _shelf.Items.Count;
            return count == 0 ? "Kosong" : $"{count} item";
        }
    }

    public bool ShelfDropHover
    {
        get => _shelfDropHover;
        set => SetProperty(ref _shelfDropHover, value);
    }

    public int AddShelfFiles(IEnumerable<string> paths)
    {
        var before = _shelf.Items.Count;
        var added = _shelf.AddMany(paths);
        if (_shelf.Items.Count != before)
        {
            RaiseShelf();
        }

        return added;
    }

    public void RemoveShelfItem(ShelfItem item)
    {
        _shelf.Remove(item);
        RaiseShelf();
    }

    public void ClearShelf()
    {
        _shelf.Clear();
        RaiseShelf();
    }

    public void OpenShelfItem(ShelfItem item)
    {
        try
        {
            if (item.IsDirectory)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{item.Path}\"") { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log.Write("Could not open shelf item.", ex);
        }
    }

    public void RevealShelfItem(ShelfItem item)
    {
        try
        {
            var args = item.IsDirectory ? $"\"{item.Path}\"" : $"/select,\"{item.Path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write("Could not reveal shelf item.", ex);
        }
    }

    private void RaiseShelf()
    {
        OnPropertyChanged(nameof(HasShelfItems));
        OnPropertyChanged(nameof(ShelfCountText));
    }

    // ----- Peek ----------------------------------------------------------

    public PeekKind PeekKind
    {
        get => _peekKind;
        private set
        {
            if (SetProperty(ref _peekKind, value))
            {
                OnPropertyChanged(nameof(PeekIsVolume));
                OnPropertyChanged(nameof(PeekIsPower));
                OnPropertyChanged(nameof(PeekIsMedia));
            }
        }
    }

    public bool PeekIsVolume => PeekKind == PeekKind.Volume;

    public bool PeekIsPower => PeekKind == PeekKind.Power;

    public bool PeekIsMedia => PeekKind == PeekKind.Media;

    public string PeekTitle
    {
        get => _peekTitle;
        private set => SetProperty(ref _peekTitle, value);
    }

    public string PeekSubtitle
    {
        get => _peekSubtitle;
        private set => SetProperty(ref _peekSubtitle, value);
    }

    public string PeekValue
    {
        get => _peekValue;
        private set => SetProperty(ref _peekValue, value);
    }

    public double PeekProgress
    {
        get => _peekProgress;
        private set => SetProperty(ref _peekProgress, value);
    }

    public ImageSource? PeekArtwork
    {
        get => _peekArtwork;
        private set => SetProperty(ref _peekArtwork, value);
    }

    public bool PeekHasArtwork => _peekArtwork is not null;

    public void FillPeek(PeekKind kind)
    {
        PeekKind = kind;
        switch (kind)
        {
            case PeekKind.Volume:
                PeekTitle = _isMuted ? "Bisu" : "Volume";
                PeekSubtitle = _isMuted ? "Suara dimatikan" : $"Level suara {_volumeValue}%";
                PeekValue = _isMuted ? "0%" : $"{_volumeValue}%";
                PeekProgress = _isMuted ? 0 : _volumeValue / 100.0;
                PeekArtwork = null;
                break;
            case PeekKind.Power:
                PeekTitle = BatteryStateText;
                PeekSubtitle = PowerPeekSubtitle;
                PeekValue = BatteryText;
                PeekProgress = BatteryProgress;
                PeekArtwork = null;
                break;
            case PeekKind.Media:
                PeekTitle = TrackTitle;
                PeekSubtitle = TrackArtist;
                PeekValue = TrackSource;
                PeekProgress = _track.Progress;
                PeekArtwork = _track.Artwork;
                break;
            default:
                PeekTitle = string.Empty;
                PeekSubtitle = string.Empty;
                PeekValue = string.Empty;
                PeekProgress = 0;
                PeekArtwork = null;
                break;
        }

        OnPropertyChanged(nameof(PeekHasArtwork));
    }

    public string PeekVolumeGlyph => _isMuted ? "mute" : "volume";

    public string PeekPowerGlyph => _lastPower.IsPlugged ? "bolt" : "battery";

    // ----- Pin / navigation ---------------------------------------------

    public bool IsPinned
    {
        get => _pinned;
        set
        {
            if (!SetProperty(ref _pinned, value))
            {
                return;
            }

            _settings.Current.PinOpen = value;
            _settings.Save();
            PinRequested?.Invoke(value);
        }
    }

    public void RequestView(IslandView view) => ViewRequested?.Invoke(view);

    public void Notify(string title, string message) => NotifyRequested?.Invoke(title, message);

    public void Dispose()
    {
        _clockTimer.Stop();
        _volumeTimer.Stop();
        _powerTimer.Stop();
        _lyricsCts?.Cancel();
        _lyrics.Dispose();
        _pomodoro.Dispose();
        _water.Dispose();
        _media.Dispose();
        _audio.Dispose();
    }
}
