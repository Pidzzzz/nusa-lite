using System.Windows.Media;

namespace NusaLite.Models;

public sealed class ShelfItem
{
    public required string Path { get; init; }

    public required string Name { get; init; }

    public required string Badge { get; init; }

    public required string SizeText { get; init; }

    public required Brush BadgeBrush { get; init; }

    public ImageSource? Thumbnail { get; init; }

    public bool IsDirectory { get; init; }

    public string Tooltip => Path;

    public string KindText => IsDirectory ? "Folder" : Badge;

    public bool ShowFolderIcon => IsDirectory && Thumbnail is null;

    public bool ShowFileIcon => !IsDirectory && Thumbnail is null;
}
