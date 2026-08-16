using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WinThunar.Models;

public sealed class FileSystemEntry : ObservableObject
{
    private ImageSource? _thumbnail;
    private int _zoomLevel = 2;

    public FileSystemEntry(
        string name,
        string fullPath,
        bool isDirectory,
        string size,
        string type,
        string modified,
        string glyph,
        long byteSize = 0,
        DateTime? modifiedTime = null)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Size = size;
        Type = type;
        Modified = modified;
        Glyph = glyph;
        ByteSize = byteSize;
        ModifiedTime = modifiedTime ?? DateTime.MinValue;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string Size { get; }
    public string Type { get; }
    public string Modified { get; }
    public string Glyph { get; }
    public long ByteSize { get; }
    public DateTime ModifiedTime { get; }
    public double IconItemWidth => 80 + (ZoomLevel * 16);
    public double IconItemHeight => 68 + (ZoomLevel * 13);
    public double IconImageSize => 28 + (ZoomLevel * 10);
    public double IconNameWidth => IconItemWidth - 16;

    public int ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            var normalized = Math.Clamp(value, 0, 4);
            if (SetProperty(ref _zoomLevel, normalized))
            {
                OnPropertyChanged(nameof(IconItemWidth));
                OnPropertyChanged(nameof(IconItemHeight));
                OnPropertyChanged(nameof(IconImageSize));
                OnPropertyChanged(nameof(IconNameWidth));
            }
        }
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (SetProperty(ref _thumbnail, value))
            {
                OnPropertyChanged(nameof(ThumbnailVisibility));
                OnPropertyChanged(nameof(GlyphVisibility));
            }
        }
    }

    public Visibility ThumbnailVisibility => Thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GlyphVisibility => Thumbnail is null ? Visibility.Visible : Visibility.Collapsed;
    public GridLength SizeColumnWidth { get; private set; } = new(110);
    public GridLength TypeColumnWidth { get; private set; } = new(140);
    public GridLength ModifiedColumnWidth { get; private set; } = new(160);
    public GridLength NameSizeGripWidth { get; private set; } = new(7);
    public GridLength SizeTypeGripWidth { get; private set; } = new(7);
    public GridLength TypeModifiedGripWidth { get; private set; } = new(7);

    public void ConfigureColumns(
        bool showSize,
        bool showType,
        bool showModified,
        double sizeWidth = 110,
        double typeWidth = 140,
        double modifiedWidth = 160)
    {
        SizeColumnWidth = new GridLength(showSize ? sizeWidth : 0);
        TypeColumnWidth = new GridLength(showType ? typeWidth : 0);
        ModifiedColumnWidth = new GridLength(showModified ? modifiedWidth : 0);
        NameSizeGripWidth = new GridLength(showSize ? 7 : 0);
        SizeTypeGripWidth = new GridLength(showSize && showType ? 7 : 0);
        TypeModifiedGripWidth = new GridLength(showType && showModified ? 7 : 0);
        OnPropertyChanged(nameof(SizeColumnWidth));
        OnPropertyChanged(nameof(TypeColumnWidth));
        OnPropertyChanged(nameof(ModifiedColumnWidth));
        OnPropertyChanged(nameof(NameSizeGripWidth));
        OnPropertyChanged(nameof(SizeTypeGripWidth));
        OnPropertyChanged(nameof(TypeModifiedGripWidth));
    }
}
