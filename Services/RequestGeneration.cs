namespace WinThunar.Services;

public sealed class RequestGeneration
{
    private int _version;

    public int Next() => Interlocked.Increment(ref _version);

    public bool IsCurrent(int version) => version == Volatile.Read(ref _version);
}
