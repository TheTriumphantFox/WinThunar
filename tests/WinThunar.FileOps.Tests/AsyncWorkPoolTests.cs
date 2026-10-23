using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class AsyncWorkPoolTests
{
    [Fact]
    public async Task LargeBatchBoundsPendingWorkAndVisitsEveryItemOnce()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var visited = new int[10000];
        var started = 0;
        var task = AsyncWorkPool.RunAsync(Enumerable.Range(0, visited.Length).ToArray(), 6, async index =>
        {
            Interlocked.Increment(ref started);
            Interlocked.Increment(ref visited[index]);
            await release.Task;
        }, CancellationToken.None);
        Assert.Equal(6, Volatile.Read(ref started));
        release.SetResult();
        await task;
        Assert.All(visited, count => Assert.Equal(1, count));
    }

    [Fact]
    public async Task CancellationStopsStartingRemainingItems()
    {
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var task = AsyncWorkPool.RunAsync(Enumerable.Range(0, 10000).ToArray(), 6, async _ =>
        {
            Interlocked.Increment(ref started);
            await release.Task;
        }, cancellation.Token);
        cancellation.Cancel();
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(6, started);
    }

    [Fact]
    public async Task FailureIsObservedAfterOtherWorkersFinish()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = 0;
        var task = AsyncWorkPool.RunAsync(new[] { 0, 1 }, 2, async index =>
        {
            if (index == 0) throw new IOException("Synthetic image failure");
            await release.Task;
            Interlocked.Increment(ref completed);
        }, CancellationToken.None);
        Assert.False(task.IsCompleted);
        release.SetResult();
        await Assert.ThrowsAsync<IOException>(() => task);
        Assert.Equal(1, completed);
    }
}
