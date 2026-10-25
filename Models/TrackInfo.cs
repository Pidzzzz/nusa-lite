using System.Windows.Media;

namespace NusaLite.Models;

public sealed record TrackInfo
{
    public static readonly TrackInfo Empty = new();

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public string SourceApp { get; init; } = string.Empty;

    public bool IsPlaying { get; init; }

    public TimeSpan Position { get; init; }

    public TimeSpan Duration { get; init; }

    public ImageSource? Artwork { get; init; }

    public bool HasTrack => !string.IsNullOrWhiteSpace(Title);

    public string Key => $"{Artist}\u001f{Title}\u001f{Album}\u001f{(int)Duration.TotalSeconds}";

    public double Progress
    {
        get
        {
            if (Duration <= TimeSpan.Zero)
            {
                return 0;
            }

            return Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
        }
    }

    public string ArtistAlbum
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Artist))
            {
                return string.IsNullOrWhiteSpace(Album) ? "Unknown artist" : Album;
            }

            return string.IsNullOrWhiteSpace(Album) ? Artist : $"{Artist} - {Album}";
        }
    }

    public static string Format(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }

    public string SourceName
    {
        get
        {
            var id = SourceApp;
            if (string.IsNullOrWhiteSpace(id))
            {
                return "Media";
            }

            if (id.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
            if (id.Contains("chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
            if (id.Contains("msedge", StringComparison.OrdinalIgnoreCase)) return "Edge";
            if (id.Contains("firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
            if (id.Contains("zunemusic", StringComparison.OrdinalIgnoreCase) || id.Contains("mediaplayer", StringComparison.OrdinalIgnoreCase)) return "Media Player";
            if (id.Contains("music.ui", StringComparison.OrdinalIgnoreCase)) return "Groove";
            if (id.Contains("foobar", StringComparison.OrdinalIgnoreCase)) return "foobar2000";
            if (id.Contains("vlc", StringComparison.OrdinalIgnoreCase)) return "VLC";
            if (id.Contains("aimp", StringComparison.OrdinalIgnoreCase)) return "AIMP";

            try
            {
                return Path.GetFileNameWithoutExtension(id);
            }
            catch
            {
                return "Media";
            }
        }
    }
}
