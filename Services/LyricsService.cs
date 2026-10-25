using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using NusaLite.Infrastructure;
using NusaLite.Models;

namespace NusaLite.Services;

public sealed class LyricsResult
{
    public static readonly LyricsResult Empty = new() { Status = LyricsStatus.NotFound };

    public LyricsStatus Status { get; init; } = LyricsStatus.NotFound;

    public IReadOnlyList<LyricLine> Lines { get; init; } = Array.Empty<LyricLine>();

    public bool HasLines => Lines.Count > 0;

    public string Message => Status switch
    {
        LyricsStatus.Loading => "Mencari lirik...",
        LyricsStatus.NotFound => "Lirik tidak ditemukan",
        LyricsStatus.Instrumental => "Lagu instrumental",
        LyricsStatus.NoMedia => "Tidak ada lagu yang diputar",
        LyricsStatus.Error => "Gagal memuat lirik",
        _ => string.Empty
    };
}

public enum LyricsStatus
{
    Loading,
    Ready,
    NotFound,
    Instrumental,
    NoMedia,
    Error
}

public sealed class LyricsService : IDisposable
{
    private static readonly Regex TimeTag = new(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, LyricsResult> _cache = new();

    public LyricsService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NusaLite/1.0 (Windows; living notch)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<LyricsResult> GetAsync(TrackInfo track, CancellationToken cancellationToken)
    {
        if (!track.HasTrack)
        {
            return new LyricsResult { Status = LyricsStatus.NoMedia };
        }

        if (_cache.TryGetValue(track.Key, out var cached))
        {
            return cached;
        }

        try
        {
            var result = await FetchAsync(track, cancellationToken);
            _cache[track.Key] = result;
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Write("Lyrics lookup failed.", ex);
            return new LyricsResult { Status = LyricsStatus.Error };
        }
    }

    private async Task<LyricsResult> FetchAsync(TrackInfo track, CancellationToken cancellationToken)
    {
        var artist = Uri.EscapeDataString(track.Artist);
        var title = Uri.EscapeDataString(track.Title);
        var album = Uri.EscapeDataString(track.Album);
        var duration = Math.Max(0, (int)Math.Round(track.Duration.TotalSeconds));

        var url = $"https://lrclib.net/api/get?artist_name={artist}&track_name={title}&album_name={album}&duration={duration}";
        using (var response = await _http.GetAsync(url, cancellationToken))
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var parsed = ParseDocument(document.RootElement);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
        }

        var searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString($"{track.Artist} {track.Title}")}";
        using (var response = await _http.GetAsync(searchUrl, cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
            {
                return LyricsResult.Empty;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return LyricsResult.Empty;
            }

            JsonElement? best = null;
            var bestScore = int.MinValue;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var score = 0;
                if (item.TryGetProperty("syncedLyrics", out var synced) && synced.ValueKind == JsonValueKind.String)
                {
                    score += 10;
                }

                var itemArtist = item.TryGetProperty("artistName", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                var itemTitle = item.TryGetProperty("trackName", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                if (itemArtist.Equals(track.Artist, StringComparison.OrdinalIgnoreCase))
                {
                    score += 3;
                }

                if (itemTitle.Equals(track.Title, StringComparison.OrdinalIgnoreCase))
                {
                    score += 3;
                }

                if (item.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number && duration > 0)
                {
                    var delta = Math.Abs(d.GetDouble() - duration);
                    if (delta < 4)
                    {
                        score += 2;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = item.Clone();
                }
            }

            if (best is not null)
            {
                using var bestDoc = JsonDocument.Parse(best.Value.GetRawText());
                var parsed = ParseDocument(bestDoc.RootElement);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
        }

        return LyricsResult.Empty;
    }

    private static LyricsResult? ParseDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("instrumental", out var instrumental) && instrumental.ValueKind == JsonValueKind.True)
        {
            return new LyricsResult { Status = LyricsStatus.Instrumental };
        }

        if (!root.TryGetProperty("syncedLyrics", out var syncedLyrics) || syncedLyrics.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = syncedLyrics.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = ParseLrc(text);
        return lines.Count == 0
            ? new LyricsResult { Status = LyricsStatus.NotFound }
            : new LyricsResult { Status = LyricsStatus.Ready, Lines = lines };
    }

    public static List<LyricLine> ParseLrc(string text)
    {
        var lines = new List<LyricLine>();

        foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            var matches = TimeTag.Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            var content = TimeTag.Replace(raw, string.Empty).Trim();
            if (content.Length == 0)
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = int.Parse(match.Groups[2].Value);
                var fractionText = match.Groups[3].Value;
                var fraction = 0.0;
                if (fractionText.Length > 0)
                {
                    fraction = int.Parse(fractionText) / Math.Pow(10, fractionText.Length);
                }

                lines.Add(new LyricLine(TimeSpan.FromSeconds(minutes * 60 + seconds + fraction), content));
            }
        }

        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    public static int FindLine(IReadOnlyList<LyricLine> lines, TimeSpan position, int offsetMs)
    {
        if (lines.Count == 0)
        {
            return -1;
        }

        var target = position - TimeSpan.FromMilliseconds(offsetMs);
        var low = 0;
        var high = lines.Count - 1;
        var result = -1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (lines[mid].Time <= target)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }

    public void Invalidate(TrackInfo track)
    {
        _cache.TryRemove(track.Key, out _);
    }

    public void Dispose() => _http.Dispose();
}
