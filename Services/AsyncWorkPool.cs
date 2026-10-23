namespace WinThunar.Services;

// Keeps continuations on the caller's context, including the UI thread for shell images.
public static class AsyncWorkPool
{
    public static async Task RunAsync<T>(IReadOnlyList<T> items, int workers,
        Func<T, Task> action, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workers, 1);
        var next = -1;
        async Task WorkAsync()
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var index = Interlocked.Increment(ref next);
                if (index >= items.Count) return;
                await action(items[index]);
            }
        }

        await Task.WhenAll(Enumerable.Range(0, Math.Min(workers, items.Count)).Select(_ => WorkAsync()));
    }
}
