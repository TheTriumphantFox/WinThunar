namespace WinThunar.Models;

public enum NavigationLocationKind
{
    FileSystem,
    RecycleBin,
    NetworkBrowser
}

public sealed record NavigationLocation(
    string Name,
    string Path,
    string Glyph,
    NavigationLocationKind Kind = NavigationLocationKind.FileSystem);
