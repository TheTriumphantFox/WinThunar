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
    NavigationLocationKind Kind = NavigationLocationKind.FileSystem)
{
    // List items without an explicit automation name are announced by ToString().
    public override string ToString() => Name;
}
