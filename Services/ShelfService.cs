using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NusaLite.Infrastructure;
using NusaLite.Models;

namespace NusaLite.Services;

public sealed class ShelfService
{
    private const int MaxItems = 12;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico"
    };

    public ObservableCollection<ShelfItem> Items { get; } = new();

    public event Action? Changed;

    public bool Add(string path)
    {
        try
        {
            if (Items.Count >= MaxItems)
            {
                return false;
            }

            var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (string.IsNullOrWhiteSpace(expanded))
            {
                return false;
            }

            var isDirectory = Directory.Exists(expanded);
            if (!isDirectory && !File.Exists(expanded))
            {
                return false;
            }

            var full = System.IO.Path.GetFullPath(expanded);
            if (Items.Any(item => string.Equals(item.Path, full, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            Items.Add(CreateItem(full, isDirectory));
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"Shelf could not add '{path}'.", ex);
            return false;
        }
    }

    public int AddMany(IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (Items.Count >= MaxItems)
            {
                break;
            }

            if (Add(path))
            {
                added++;
            }
        }

        return added;
    }

    public void Remove(ShelfItem item)
    {
        if (Items.Remove(item))
        {
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        if (Items.Count == 0)
        {
            return;
        }

        Items.Clear();
        Changed?.Invoke();
    }

    private static ShelfItem CreateItem(string path, bool isDirectory)
    {
        var name = isDirectory
            ? new DirectoryInfo(path).Name
            : System.IO.Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = path;
        }

        var extension = isDirectory ? string.Empty : System.IO.Path.GetExtension(path);
        var badge = isDirectory ? "DIR" : extension.TrimStart('.').ToUpperInvariant();
        if (badge.Length > 4)
        {
            badge = badge[..4];
        }

        if (badge.Length == 0)
        {
            badge = "FILE";
        }

        var sizeText = isDirectory
            ? "Folder"
            : FormatSize(new FileInfo(path).Length);

        ImageSource? thumb = null;
        if (!isDirectory && ImageExtensions.Contains(extension))
        {
            thumb = TryLoadThumbnail(path);
        }

        return new ShelfItem
        {
            Path = path,
            Name = name,
            Badge = badge,
            SizeText = sizeText,
            BadgeBrush = BrushForExtension(extension, isDirectory),
            Thumbnail = thumb,
            IsDirectory = isDirectory
        };
    }

    private static ImageSource? TryLoadThumbnail(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 120;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    private static Brush BrushForExtension(string extension, bool isDirectory)
    {
        var key = isDirectory ? "dir" : extension.TrimStart('.').ToLowerInvariant();
        var (start, end) = key switch
        {
            "dir" => ("#5C6BC0", "#3949AB"),
            "png" or "jpg" or "jpeg" or "gif" or "bmp" or "webp" or "tif" or "tiff" or "ico" => ("#26C6DA", "#00838F"),
            "pdf" => ("#FF7043", "#D84315"),
            "doc" or "docx" or "rtf" => ("#4FC3F7", "#1565C0"),
            "xls" or "xlsx" or "csv" => ("#66BB6A", "#2E7D32"),
            "ppt" or "pptx" => ("#FF8A65", "#BF360C"),
            "zip" or "rar" or "7z" or "tar" or "gz" => ("#FFD54F", "#EF6C00"),
            "mp3" or "wav" or "flac" or "m4a" or "ogg" => ("#9575CD", "#4527A0"),
            "mp4" or "mkv" or "mov" or "avi" or "webm" => ("#F06292", "#AD1457"),
            "exe" or "msi" => ("#90A4AE", "#455A64"),
            _ => ("#42A5F5", "#1565C0")
        };

        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(start),
            (Color)ColorConverter.ConvertFromString(end),
            145);
        brush.Freeze();
        return brush;
    }
}
