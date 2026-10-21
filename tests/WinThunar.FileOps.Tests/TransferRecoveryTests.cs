using System.Text.Json.Nodes;
using WinThunar.Services;
using Xunit;

namespace WinThunar.FileOps.Tests;

public sealed class TransferRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinThunar.FileOps.Tests", Guid.NewGuid().ToString("N"));
    private string Journal => Path.Combine(_root, "journal");

    [Fact]
    public void SafeRetryCanRecoverAFailedAttemptOwnedByThisProcess()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.txt");
        var destination = Path.Combine(_root, "destination.txt");
        var stage = Path.Combine(_root, ".winthunar-incoming-fixture.tmp");
        File.WriteAllText(source, "original source");
        File.WriteAllText(stage, "partial data");
        var service = new TransferRecoveryService(Journal);
        var id = service.Register(source, destination, stage);
        var recordPath = Path.Combine(Journal, id + ".json");
        var record = JsonNode.Parse(File.ReadAllText(recordPath))!;
        record["OwnerProcessId"] = 0; // Model a transfer whose process exited.
        File.WriteAllText(recordPath, record.ToJsonString());

        using (var locked = new FileStream(stage, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = service.RecoverPending(skipActiveProcesses: true);
            Assert.Equal(0, failed.RecoveredOperations);
            Assert.Single(failed.Errors);
            var issue = Assert.Single(failed.Issues);
            Assert.Equal(source, issue.SourcePath);
            Assert.Equal(destination, issue.DestinationPath);
            Assert.Equal(stage, issue.StagedPath);
            Assert.Equal(recordPath, issue.JournalPath);
            Assert.True(File.Exists(recordPath));
        }

        // A fresh service models another window. It must not mistake the failed claim for a
        // still-running transfer, even though both attempts belong to this process.
        var retry = new TransferRecoveryService(Journal).RecoverPending(skipActiveProcesses: true);
        Assert.Empty(retry.Errors);
        Assert.Equal(1, retry.RecoveredOperations);
        Assert.False(File.Exists(stage));
        Assert.False(File.Exists(recordPath));
        Assert.Equal("original source", File.ReadAllText(source));
        Assert.Equal(0, service.RecoverPending(skipActiveProcesses: true).RecoveredOperations);
    }

    [Fact]
    public void LiveTransferAndRetainedHistoryAreCountedAndLeftAlone()
    {
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "destination.txt");
        var stage = Path.Combine(_root, ".winthunar-staging-fixture.tmp");
        File.WriteAllText(stage, "live transfer");
        var service = new TransferRecoveryService(Journal);
        service.Register(destination, destination, stage);
        var retained = service.Register(destination, destination, Path.Combine(_root, ".winthunar-history-fixture.tmp"));
        service.CommitRetained(retained);

        var result = new TransferRecoveryService(Journal).RecoverPending(skipActiveProcesses: true);

        Assert.Equal(2, result.ActiveOperations);
        Assert.Equal(0, result.RecoveredOperations);
        Assert.Empty(result.Errors);
        Assert.Equal("live transfer", File.ReadAllText(stage));
        Assert.Equal(2, Directory.GetFiles(Journal, "*.json").Length);
    }

    [Fact]
    public void CorruptRecordIsReportedWithItsPathAndPreserved()
    {
        Directory.CreateDirectory(Journal);
        var recordPath = Path.Combine(Journal, "broken.json");
        File.WriteAllText(recordPath, "{ broken JSON");

        var result = new TransferRecoveryService(Journal).RecoverPending(skipActiveProcesses: true);

        Assert.Single(result.Errors);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(recordPath, issue.JournalPath);
        Assert.Null(issue.SourcePath);
        Assert.Equal("{ broken JSON", File.ReadAllText(recordPath));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RecoveryOnlyDiscardsDisplacedCopiesWhenTheCompletedDestinationIsProven(bool prepared, bool editedStream)
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.txt");
        var destination = Path.Combine(_root, "destination.txt");
        var stage = Path.Combine(_root, ".winthunar-incoming-fixture.tmp");
        var backup = Path.Combine(_root, ".winthunar-backup-fixture.tmp");
        var retainedSource = Path.Combine(_root, ".winthunar-source-fixture.tmp");
        File.WriteAllText(destination, "promoted content");
        File.WriteAllText(destination + ":notes", "original stream");
        File.WriteAllText(backup, "previous destination");
        File.WriteAllText(retainedSource, "original incoming source");
        var service = new TransferRecoveryService(Journal);
        var id = service.Register(source, destination, stage, backup, retainedSource);
        if (prepared) service.RecordPreparedDestination(id, FileOperationService.CapturePathState(destination));
        if (editedStream)
        {
            var modified = File.GetLastWriteTimeUtc(destination);
            File.WriteAllText(destination + ":notes", "edited after the interruption");
            File.SetLastWriteTimeUtc(destination, modified);
        }

        var result = service.RecoverPending();

        Assert.Equal("promoted content", File.ReadAllText(destination));
        if (prepared && !editedStream)
        {
            Assert.Empty(result.Errors);
            Assert.Equal(1, result.RecoveredOperations);
            Assert.False(File.Exists(backup));
            Assert.False(File.Exists(retainedSource));
        }
        else
        {
            Assert.Single(result.Errors);
            Assert.Single(result.Issues);
            Assert.Equal("previous destination", File.ReadAllText(backup));
            Assert.Equal("original incoming source", File.ReadAllText(retainedSource));
            Assert.True(File.Exists(Path.Combine(Journal, id + ".json")));
        }
    }

    [Fact]
    public void MissingParentKeepsItsJournalUntilStorageIsAvailable()
    {
        Directory.CreateDirectory(_root);
        var destinationParent = Path.Combine(_root, "unavailable-storage");
        var destination = Path.Combine(destinationParent, "item.txt");
        var stage = Path.Combine(destinationParent, ".winthunar-incoming-fixture.tmp");
        var source = Path.Combine(_root, "source.txt");
        File.WriteAllText(source, "incoming");
        var service = new TransferRecoveryService(Journal);
        var id = service.Register(source, destination, stage);

        var missing = service.RecoverPending();
        Assert.Single(missing.Errors);
        Assert.True(File.Exists(Path.Combine(Journal, id + ".json")));

        Directory.CreateDirectory(destinationParent); // Model reconnecting the recorded path.
        File.WriteAllText(stage, "partial data on the reconnected storage");
        var retry = service.RecoverPending(skipActiveProcesses: true);
        Assert.Empty(retry.Errors);
        Assert.Equal(1, retry.RecoveredOperations);
        Assert.False(File.Exists(stage));
        Assert.Equal("incoming", File.ReadAllText(source));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) LinkFileSystem.Delete(_root);
    }
}
