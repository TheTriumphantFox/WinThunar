namespace WinThunar.Models;

public enum NavigationLocationKind
{
    FileSystem,
    Computer,
    RecycleBin,
    NetworkBrowser
}

public sealed record NavigationLocation(
    string Name,
    string Path,
    string Glyph,
    NavigationLocationKind Kind = NavigationLocationKind.FileSystem);
