using System.Collections.Concurrent;
using WinThunar.Models;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class FileTransferQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictWaitKeepsCallerResponsiveAndSecondTransferQueued(bool cancelFirst)
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var source = Path.Combine(sourceDirectory, "first.txt");
        var second = Path.Combine(sourceDirectory, "second.txt");
        File.WriteAllText(source, "incoming");
        File.WriteAllText(second, "second");
        File.WriteAllText(Path.Combine(destination, "first.txt"), "existing");
        using var context = new PumpContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var ownerThread = Environment.CurrentManagedThreadId;
            using var queue = new FileTransferQueue(new FileOperationService(
                new TransferRecoveryService(Path.Combine(_root, "recovery"))));
            var decision = new TaskCompletionSource<ConflictResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
            var prompted = false;
            var first = queue.EnqueueAsync([source], destination, FileTransferMode.Copy, _ =>
            {
                Assert.Equal(ownerThread, Environment.CurrentManagedThreadId);
                prompted = true;
                return decision.Task;
            });
            // Filesystem work and conflict handling must not happen inline in EnqueueAsync.
            Assert.False(prompted);
            Assert.False(first.IsCompleted);
            var next = queue.EnqueueAsync([second], destination, FileTransferMode.Copy,
                _ => throw new InvalidOperationException("Unexpected second conflict"));
            context.PumpUntil(() => prompted);
            Assert.Equal(1, queue.QueuedCount);
            Assert.Equal(FileTransferJobState.Queued, queue.Jobs[1].State);
            Assert.False(File.Exists(Path.Combine(destination, "second.txt")));

            if (cancelFirst) queue.CancelActive();
            decision.SetResult(new ConflictResolution(ConflictAction.Replace));
            context.PumpUntil(() => first.IsCompleted && next.IsCompleted && queue.ActiveJob is null);
            var firstResult = await first;
            var nextResult = await next;
            Assert.Equal(cancelFirst, firstResult.Cancelled);
            Assert.True(nextResult.Succeeded, string.Join("; ", nextResult.Errors));
            Assert.Equal(cancelFirst ? "existing" : "incoming", File.ReadAllText(Path.Combine(destination, "first.txt")));
            Assert.Equal("second", File.ReadAllText(Path.Combine(destination, "second.txt")));
            Assert.Equal(0, queue.QueuedCount);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private sealed class PumpContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _work = new();
        public override void Post(SendOrPostCallback callback, object? state) => _work.Add((callback, state));

        public void PumpUntil(Func<bool> done)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!done())
            {
                Assert.True(DateTime.UtcNow < deadline, "Timed out pumping the caller's context.");
                if (_work.TryTake(out var work, 100)) work.Callback(work.State);
            }
            while (_work.TryTake(out var remaining)) remaining.Callback(remaining.State);
        }

        public void Dispose() => _work.Dispose();
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
