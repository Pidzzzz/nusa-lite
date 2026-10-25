using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NusaLite.Infrastructure;
using NusaLite.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NusaLite.Services;

public sealed class MediaService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ArtworkService _artworkResolver = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private DispatcherTimer? _positionTimer;
    private CancellationTokenSource? _artworkCancellation;
    private string _artworkKey = string.Empty;
    private string _artworkFetchKey = string.Empty;
    private TrackInfo _current = TrackInfo.Empty;
    private bool _disposed;

    private TimeSpan _lastReportedPosition;
    private TimeSpan _clockPosition;
    private DateTimeOffset _clockAt;
    private bool _clockInitialized;
    private bool _clockWasPlaying;

    public MediaService()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public event Action? Changed;

    public TrackInfo Current => _current;

    public bool IsReady => _manager is not null;

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;

            _positionTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _positionTimer.Tick += (_, _) => UpdateFromSession();
            _positionTimer.Start();

            var current = _manager.GetCurrentSession() ?? GetPreferredSession();
            Attach(current);
        }
        catch (Exception ex)
        {
            Log.Write("Media session manager could not be created.", ex);
        }
    }

    private GlobalSystemMediaTransportControlsSession? GetPreferredSession()
    {
        try
        {
            var sessions = _manager?.GetSessions();
            if (sessions is null)
            {
                return null;
            }

            foreach (var session in sessions)
            {
                try
                {
                    if (session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        return session;
                    }
                }
                catch
                {
                    // Skip sessions that disappear mid-enumeration.
                }
            }

            return sessions.Count > 0 ? sessions[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _dispatcher.BeginInvoke(() => Attach(sender.GetCurrentSession() ?? GetPreferredSession()));
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        if (ReferenceEquals(_session, session))
        {
            return;
        }

        DetachEvents();

        _session = session;
        _artworkKey = string.Empty;
        CancelArtworkFetch();
        ResetPositionClock();

        if (_session is null)
        {
            _current = TrackInfo.Empty;
            Changed?.Invoke();
            return;
        }

        try
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }
        catch (Exception ex)
        {
            Log.Write("Could not subscribe to the media session.", ex);
        }

        _ = RefreshMetadataAsync();
    }

    private void DetachEvents()
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }
        catch
        {
            // The session may already be gone.
        }

        _session = null;
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        => _ = SafeRefreshAsync();

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        => UpdateFromSession();

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        => UpdateFromSession();

    private async Task SafeRefreshAsync()
    {
        try
        {
            await _dispatcher.InvokeAsync(RefreshMetadataAsync).Task.Unwrap();
        }
        catch (Exception ex)
        {
            Log.Write("Media metadata refresh failed.", ex);
        }
    }

    private async Task RefreshMetadataAsync()
    {
        var session = _session;
        if (session is null || _disposed)
        {
            return;
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            if (properties is null)
            {
                return;
            }

            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var status = playback?.PlaybackStatus ?? GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;

            if (!ReferenceEquals(_session, session) || _disposed)
            {
                return;
            }

            var title = properties.Title ?? string.Empty;
            var artist = properties.Artist ?? string.Empty;
            var sourceApp = session.SourceAppUserModelId ?? string.Empty;
            var key = $"{artist}\u001f{title}";
            ImageSource? artwork = _current.Artwork;

            if (key != _artworkKey)
            {
                _artworkKey = key;
                ResetPositionClock();
                CancelArtworkFetch();

                if (IsBrowserSource(sourceApp))
                {
                    artwork = _artworkResolver.TryGetCached(title, artist, out var cached)
                        ? cached
                        : null;
                }
                else
                {
                    artwork = await LoadArtworkAsync(properties);
                }

                if (artwork is null)
                {
                    _artworkCancellation = new CancellationTokenSource();
                    _artworkFetchKey = key;
                    _ = FetchOnlineArtworkAsync(
                        session,
                        key,
                        title,
                        artist,
                        _artworkCancellation.Token);
                }
            }

            if (!ReferenceEquals(_session, session) || _disposed)
            {
                return;
            }

            var duration = timeline is null
                ? _current.Duration
                : timeline.EndTime - timeline.StartTime;
            if (duration <= TimeSpan.Zero && status != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
            {
                duration = _current.Duration;
            }

            var isPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var position = timeline is null
                ? _current.Position
                : ResolvePosition(timeline, duration, isPlaying);

            _current = new TrackInfo
            {
                Title = title,
                Artist = artist,
                Album = properties.AlbumTitle ?? string.Empty,
                SourceApp = sourceApp,
                IsPlaying = isPlaying,
                Position = position,
                Duration = duration,
                Artwork = artwork
            };

            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Write("Reading the media session failed.", ex);
            if (_session is null)
            {
                _current = TrackInfo.Empty;
                Changed?.Invoke();
            }
        }
    }

    private async Task FetchOnlineArtworkAsync(
        GlobalSystemMediaTransportControlsSession session,
        string key,
        string title,
        string artist,
        CancellationToken token)
    {
        try
        {
            var artwork = await _artworkResolver.FindAsync(title, artist, token);
            if (artwork is null || token.IsCancellationRequested)
            {
                return;
            }

            if (_disposed ||
                !ReferenceEquals(_session, session) ||
                _artworkKey != key ||
                _artworkFetchKey != key)
            {
                return;
            }

            _current = _current with { Artwork = artwork };
            Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // A newer track replaced this lookup.
        }
        catch (Exception ex)
        {
            Log.Write("Online artwork lookup failed.", ex);
        }
    }

    private void CancelArtworkFetch()
    {
        _artworkCancellation?.Cancel();
        _artworkCancellation?.Dispose();
        _artworkCancellation = null;
        _artworkFetchKey = string.Empty;
    }

    private static bool IsBrowserSource(string sourceApp)
    {
        return sourceApp.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
               sourceApp.Contains("chromium", StringComparison.OrdinalIgnoreCase) ||
               sourceApp.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
               sourceApp.Contains("edge", StringComparison.OrdinalIgnoreCase) ||
               sourceApp.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
               sourceApp.Contains("brave", StringComparison.OrdinalIgnoreCase) ||
               sourceApp.Contains("opera", StringComparison.OrdinalIgnoreCase) ||
               sourceApp.Contains("vivaldi", StringComparison.OrdinalIgnoreCase);
    }

    private void ResetPositionClock()
    {
        _lastReportedPosition = TimeSpan.Zero;
        _clockPosition = TimeSpan.Zero;
        _clockAt = default;
        _clockInitialized = false;
        _clockWasPlaying = false;
    }

    private TimeSpan ResolvePosition(
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline,
        TimeSpan duration,
        bool isPlaying)
    {
        var raw = ClampPosition(timeline.Position - timeline.StartTime, duration);
        var now = DateTimeOffset.Now;

        if (!_clockInitialized)
        {
            _clockInitialized = true;
            _lastReportedPosition = raw;
            _clockPosition = raw;
            _clockAt = now;
            _clockWasPlaying = isPlaying;
            return raw;
        }

        if (raw != _lastReportedPosition)
        {
            _lastReportedPosition = raw;
            _clockPosition = raw;
        }
        else if (isPlaying && _clockWasPlaying)
        {
            var elapsed = now - _clockAt;
            if (elapsed > TimeSpan.Zero)
            {
                _clockPosition += elapsed;
            }
        }

        _clockAt = now;
        _clockWasPlaying = isPlaying;
        return ClampPosition(_clockPosition, duration);
    }

    private static TimeSpan ClampPosition(TimeSpan position, TimeSpan duration)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (duration > TimeSpan.Zero && position > duration)
        {
            return duration;
        }

        return position;
    }

    private static async Task<ImageSource?> LoadArtworkAsync(GlobalSystemMediaTransportControlsSessionMediaProperties properties)
    {
        try
        {
            var reference = properties.Thumbnail;
            if (reference is null)
            {
                return null;
            }

            using var stream = await reference.OpenReadAsync();
            var length = stream.Size;
            if (length == 0 || length > 24 * 1024 * 1024)
            {
                return null;
            }

            var bytes = new byte[length];
            using (var reader = new DataReader(stream.GetInputStreamAt(0)))
            {
                await reader.LoadAsync((uint)length);
                reader.ReadBytes(bytes);
            }

            return ImageDecoder.FromBytes(bytes);
        }
        catch (Exception ex)
        {
            Log.Write("Album artwork could not be decoded.", ex);
            return null;
        }
    }

    private void UpdateFromSession()
    {
        var session = _session;
        if (session is null || _disposed)
        {
            return;
        }

        try
        {
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var duration = timeline.EndTime - timeline.StartTime;
            if (duration <= TimeSpan.Zero)
            {
                duration = _current.Duration;
            }

            var position = ResolvePosition(timeline, duration, isPlaying);
            if (position == _current.Position && isPlaying == _current.IsPlaying && duration == _current.Duration)
            {
                return;
            }

            _current = _current with
            {
                Position = position,
                Duration = duration,
                IsPlaying = isPlaying
            };

            Changed?.Invoke();
        }
        catch
        {
            // Session closed; the manager will report the next session.
        }
    }

    public async Task TogglePlayAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        try
        {
            var status = session.GetPlaybackInfo().PlaybackStatus;
            if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                await session.TryPauseAsync();
            }
            else
            {
                await session.TryPlayAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Write("Play/pause failed.", ex);
        }
    }

    public async Task NextAsync()
    {
        try
        {
            if (_session is not null)
            {
                await _session.TrySkipNextAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Write("Skip next failed.", ex);
        }
    }

    public async Task PreviousAsync()
    {
        try
        {
            if (_session is not null)
            {
                await _session.TrySkipPreviousAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Write("Skip previous failed.", ex);
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        try
        {
            if (_session is not null)
            {
                await _session.TryChangePlaybackPositionAsync(position.Ticks);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Seek failed.", ex);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        CancelArtworkFetch();
        _positionTimer?.Stop();
        _positionTimer = null;

        if (_manager is not null)
        {
            try
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }
            catch
            {
                // Ignore shutdown races.
            }

            _manager = null;
        }

        DetachEvents();
    }
}
