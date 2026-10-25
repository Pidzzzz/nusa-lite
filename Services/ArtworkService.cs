using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using NusaLite.Infrastructure;

namespace NusaLite.Services;

public sealed class ArtworkService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex Brackets = new(@"\s*[\(\[\{][^\)\]\}]*[\)\]\}]", RegexOptions.Compiled);
    private static readonly Regex Suffix = new(
        @"\s*[-–—]\s*(?:official|lyric|lyrics|audio|video|visualizer|remaster(?:ed)?|live|cover|mv)\b.*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Punctuation = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ArtSize = new(@"\d+x\d+bb", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetCached(string title, string artist, out ImageSource? artwork)
    {
        var (_, _, titleKey, artistKey) = ParseQuery(title, artist);
        if (_cache.TryGetValue(CacheKey(titleKey, artistKey), out var cached))
        {
            artwork = cached;
            return true;
        }

        artwork = null;
        return false;
    }

    public async Task<ImageSource?> FindAsync(string title, string artist, CancellationToken token)
    {
        var (searchTitle, searchArtist, titleKey, artistKey) = ParseQuery(title, artist);
        if (titleKey.Length == 0)
        {
            return null;
        }

        var cacheKey = CacheKey(titleKey, artistKey);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var candidates = await SearchDeezerAsync(searchTitle, searchArtist, token);
        var best = PickBest(candidates, titleKey, artistKey);

        if (best is null)
        {
            candidates = await SearchItunesAsync(searchTitle, searchArtist, token);
            best = PickBest(candidates, titleKey, artistKey);
        }

        if (best is null)
        {
            return null;
        }

        var artwork = await DownloadAsync(best.ArtworkUrl, token);
        if (artwork is not null)
        {
            _cache[cacheKey] = artwork;
        }

        return artwork;
    }

    private static ArtworkCandidate? PickBest(
        IReadOnlyList<ArtworkCandidate> candidates,
        string title,
        string artist)
    {
        ArtworkCandidate? best = null;
        var bestScore = 3;

        foreach (var candidate in candidates)
        {
            var score = Score(candidate, title, artist);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static int Score(ArtworkCandidate candidate, string title, string artist)
    {
        var candidateTitle = Normalize(candidate.Title);
        var candidateArtist = Normalize(candidate.Artist);
        if (candidateTitle.Length == 0)
        {
            return 0;
        }

        var titleScore = candidateTitle == title
            ? 3
            : candidateTitle.Contains(title, StringComparison.Ordinal) ||
              title.Contains(candidateTitle, StringComparison.Ordinal)
                ? 2
                : TokenOverlap(candidateTitle, title) >= 0.7
                    ? 1
                    : 0;

        if (artist.Length == 0)
        {
            return titleScore == 3 ? 4 : 0;
        }

        var artistScore = candidateArtist.Length == 0
            ? 0
            : candidateArtist == artist
                ? 2
                : candidateArtist.Contains(artist, StringComparison.Ordinal) ||
                  artist.Contains(candidateArtist, StringComparison.Ordinal)
                    ? 1
                    : 0;

        return titleScore == 0 || artistScore == 0 ? 0 : titleScore + artistScore;
    }

    private static double TokenOverlap(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        var common = leftTokens.Count(rightTokens.Contains);
        return common / (double)Math.Min(leftTokens.Count, rightTokens.Count);
    }

    private static async Task<List<ArtworkCandidate>> SearchDeezerAsync(
        string title,
        string artist,
        CancellationToken token)
    {
        var query = Uri.EscapeDataString(string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}");
        var json = await GetStringAsync($"https://api.deezer.com/search?q={query}&limit=8", token);
        var results = new List<ArtworkCandidate>();
        if (json is null)
        {
            return results;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var item in data.EnumerateArray())
            {
                var track = item.TryGetProperty("title", out var titleElement)
                    ? titleElement.GetString()
                    : null;
                var artistName = item.TryGetProperty("artist", out var artistElement) &&
                                 artistElement.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : null;
                var cover = item.TryGetProperty("album", out var albumElement) &&
                            albumElement.TryGetProperty("cover_big", out var coverElement)
                    ? coverElement.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(track) &&
                    !string.IsNullOrWhiteSpace(artistName) &&
                    !string.IsNullOrWhiteSpace(cover))
                {
                    results.Add(new ArtworkCandidate(track, artistName, cover));
                }
            }
        }
        catch (JsonException ex)
        {
            Log.Write("Deezer artwork response could not be parsed.", ex);
        }

        return results;
    }

    private static async Task<List<ArtworkCandidate>> SearchItunesAsync(
        string title,
        string artist,
        CancellationToken token)
    {
        var query = Uri.EscapeDataString(string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}");
        var json = await GetStringAsync(
            $"https://itunes.apple.com/search?term={query}&media=music&entity=song&limit=8",
            token);
        var results = new List<ArtworkCandidate>();
        if (json is null)
        {
            return results;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("results", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var item in items.EnumerateArray())
            {
                var track = item.TryGetProperty("trackName", out var titleElement)
                    ? titleElement.GetString()
                    : null;
                var artistName = item.TryGetProperty("artistName", out var artistElement)
                    ? artistElement.GetString()
                    : null;
                var cover = item.TryGetProperty("artworkUrl100", out var coverElement)
                    ? coverElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(track) ||
                    string.IsNullOrWhiteSpace(artistName) ||
                    string.IsNullOrWhiteSpace(cover))
                {
                    continue;
                }

                results.Add(new ArtworkCandidate(
                    track,
                    artistName,
                    ArtSize.Replace(cover, "600x600bb")));
            }
        }
        catch (JsonException ex)
        {
            Log.Write("iTunes artwork response could not be parsed.", ex);
        }

        return results;
    }

    private static async Task<string?> GetStringAsync(string url, CancellationToken token)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Write("Artwork search failed.", ex);
            return null;
        }
    }

    private static async Task<ImageSource?> DownloadAsync(string url, CancellationToken token)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength is > 8 * 1024 * 1024)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            if (bytes.Length == 0 || bytes.Length > 8 * 1024 * 1024)
            {
                return null;
            }

            return ImageDecoder.FromBytes(bytes);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Write("Artwork download failed.", ex);
            return null;
        }
    }

    private static (string SearchTitle, string SearchArtist, string TitleKey, string ArtistKey) ParseQuery(
        string title,
        string artist)
    {
        var searchTitle = CleanForSearch(RemoveSuffixes(title));
        var searchArtist = CleanForSearch(PrimaryArtist(artist));
        return (searchTitle, searchArtist, Normalize(searchTitle), Normalize(searchArtist));
    }

    private static string RemoveSuffixes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutBrackets = Brackets.Replace(value, " ");
        var withoutSuffix = Suffix.Replace(withoutBrackets, " ");
        return Whitespace.Replace(withoutSuffix, " ").Trim();
    }

    private static string PrimaryArtist(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var separator = value.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 ? value[..separator] : value;
    }

    private static string CleanForSearch(string value)
    {
        var cleaned = Punctuation.Replace(value ?? string.Empty, " ");
        return Whitespace.Replace(cleaned, " ").Trim();
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return CleanForSearch(builder.ToString());
    }

    private static string CacheKey(string title, string artist) => $"{artist}\u001f{title}";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NusaLite/1.0 (Windows)");
        return client;
    }

    private sealed record ArtworkCandidate(string Title, string Artist, string ArtworkUrl);
}
