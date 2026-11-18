using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using NusaLite.Infrastructure;

namespace NusaLite.Services;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateInfo(Version Version, string Tag, string HtmlUrl)
{
    public string DisplayTag => Tag.StartsWith('v') || Tag.StartsWith('V') ? Tag : $"v{Tag}";
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Release)
{
    public static UpdateCheckResult UpToDate { get; } = new(UpdateCheckStatus.UpToDate, null);

    public static UpdateCheckResult Failed { get; } = new(UpdateCheckStatus.Failed, null);
}

public sealed class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Pidzzzz/nusa-lite/releases/latest";

    public static Version CurrentVersion { get; } = ReadCurrentVersion();

    private static readonly HttpClient Client = CreateClient();

    public async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var response = await Client
                .GetAsync(LatestReleaseUrl, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UpdateCheckResult.UpToDate;
            }

            if (!response.IsSuccessStatusCode)
            {
                Log.Write($"Update check returned HTTP {(int)response.StatusCode}.");
                return UpdateCheckResult.Failed;
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var url = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

            if (!TryParseVersion(tag, out var version))
            {
                Log.Write($"Update check could not parse release tag '{tag}'.");
                return UpdateCheckResult.Failed;
            }

            if (!IsHttpGetUrl(url))
            {
                Log.Write("Update check received an invalid release page URL.");
                return UpdateCheckResult.Failed;
            }

            var release = new UpdateInfo(version, tag!.Trim(), url!);
            return version.CompareTo(CurrentVersion) > 0
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, release)
                : UpdateCheckResult.UpToDate;
        }
        catch (Exception ex)
        {
            Log.Write("Update check failed.", ex);
            return UpdateCheckResult.Failed;
        }
    }

    public static void OpenReleasePage(string url)
    {
        if (!IsHttpGetUrl(url))
        {
            Log.Write("Release page URL was rejected.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write("Release page could not be opened.", ex);
        }
    }

    private static Version ReadCurrentVersion()
    {
        var informational = typeof(UpdateService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (TryParseVersion(informational, out var version))
        {
            return version;
        }

        Log.Write($"Application informational version '{informational}' could not be parsed.");
        return new Version(0, 0, 0);
    }

    private static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            text = text[..plus];
        }

        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            text = text[..dash];
        }

        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        if (text.Count(c => c == '.') < 1)
        {
            text += ".0";
        }

        if (Version.TryParse(text, out var parsed))
        {
            version = parsed;
            return true;
        }

        return false;
    }

    private static bool IsHttpGetUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"NusaLite/{CurrentVersion} (+https://github.com/Pidzzzz/nusa-lite)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
}
