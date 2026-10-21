namespace WinThunar.Services;

// Linked only into the stress harness. Normal application builds cannot enable these hooks.
internal sealed record FileOperationFaultPoint(string Phase, string Source, string Destination, long Bytes = 0, long TotalBytes = 0);

internal static class FileOperationFaultHooks
{
    private static readonly AsyncLocal<Settings?> Current = new();
    internal sealed record Settings(bool ForceCopyDelete = false, Action<FileOperationFaultPoint>? Checkpoint = null,
        Func<FileOperationFaultPoint, int?>? CopyError = null);

    internal static bool ForceCopyDelete => Current.Value?.ForceCopyDelete == true;
    internal static void Hit(string phase, string source, string destination, long bytes = 0, long total = 0)
        => Current.Value?.Checkpoint?.Invoke(new(phase, source, destination, bytes, total));
    internal static int? CopyError(string source, string destination, long bytes, long total)
        => Current.Value?.CopyError?.Invoke(new("NativeCopyProgress", source, destination, bytes, total));

    internal sealed class Scope : IDisposable
    {
        private readonly Settings? _previous;
        internal Scope(Settings settings) { _previous = Current.Value; Current.Value = settings; }
        public void Dispose() => Current.Value = _previous;
    }
}
