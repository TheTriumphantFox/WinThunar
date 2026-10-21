using System.Diagnostics;
using System.Text.Json;
using WinThunar.Services;

namespace WinThunar.FileOps.Stress;

internal sealed record DisposableShare(string Token, string ShareName, string LocalPath, string UncPath, string Owner, string ScriptPath)
{
    internal static DisposableShare Open(string manifestPath)
    {
        var share = JsonSerializer.Deserialize<DisposableShare>(File.ReadAllText(manifestPath))
            ?? throw new InvalidDataException("The disposable-share manifest is missing.");
        var run = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var project = Path.GetDirectoryName(Path.GetDirectoryName(run))!;
        Fixture.Check(Guid.TryParseExact(share.Token, "N", out _) && share.ShareName == "WinThunar-Test-" + share.Token &&
            share.UncPath == @"\\localhost\" + share.ShareName && share.LocalPath == Path.Combine(run, "share") &&
            share.ScriptPath == Path.Combine(project, "scripts", "Test-FileOperationsSmb.ps1"), "Invalid disposable-share capability.");
        Fixture.Check(!LinkFileSystem.IsReparsePoint(run) && !LinkFileSystem.IsReparsePoint(share.LocalPath) &&
            File.ReadAllText(Path.Combine(share.LocalPath, "smb-owner.txt")) == share.Token, "Disposable-share ownership changed.");
        return share;
    }

    internal void Control(string action, string manifestPath)
    {
        _ = Open(manifestPath); // Recheck ownership before requesting a share mutation.
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ScriptPath,
                     "-ShareAction", action, "-RunDirectory", Path.GetDirectoryName(Path.GetFullPath(manifestPath))! })
            start.ArgumentList.Add(argument);
        using var worker = Process.Start(start) ?? throw new IOException("Could not start the owned-share worker.");
        if (!worker.WaitForExit(30000)) { worker.Kill(entireProcessTree: true); worker.WaitForExit(); throw new TimeoutException("Owned-share worker timed out."); }
        Fixture.Check(worker.ExitCode == 0, "Owned-share control failed: " + action);
    }
}

internal static class SmbScenarios
{
    internal static async Task<int> RunAsync(string manifestPath, string output)
    {
        var share = DisposableShare.Open(manifestPath);
        var root = Path.GetFullPath(output);
        Fixture.Check(Path.GetDirectoryName(root) == Path.GetDirectoryName(Path.GetFullPath(manifestPath)) &&
            Path.GetFileName(root) == "stress" && !Directory.Exists(root), "SMB output must be a new sibling of its manifest.");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "run-id.txt"), Guid.NewGuid().ToString("N"));
        var results = new List<ScenarioResult>();
        void SaveReport() => File.WriteAllText(Path.Combine(root, "report.json"), JsonSerializer.Serialize(new
        {
            updatedUtc = DateTimeOffset.UtcNow, passed = results.Count(r => r.Status == "Passed"), failed = results.Count(r => r.Status == "Failed"),
            scenarios = results, limits = "Loopback SMB on one physical device with an elevated token. Does not simulate a remote server restart, unplugged cable, Wi-Fi loss, or network timeout."
        }, Fixture.JsonOptions));
        foreach (var mode in new[] { FileTransferMode.Copy, FileTransferMode.Move })
            foreach (var replace in new[] { false, true })
            {
                var name = $"smb-disconnect-{mode.ToString().ToLowerInvariant()}-{(replace ? "replace" : "new")}";
                var timer = Stopwatch.StartNew();
                Fixture? fixture = null;
                try
                {
                    fixture = Fixture.Create(root, name, replace, secondVolumeRoot: share.UncPath);
                    var disconnected = false;
                    long progress = 0;
                    int? nativeError = null;
                    FileOperationResult result;
                    try
                    {
                        using var observation = new FileOperationFaultHooks.Scope(new(ForceCopyDelete: true, Checkpoint: point =>
                        {
                            if (point.Phase == "NativeCopyFailed") nativeError = (int)point.Bytes;
                            if (point.Phase != "NativeCopyProgress" || point.Bytes < 64 * 1024 || disconnected) return;
                            Fixture.Check(point.Destination.StartsWith(share.UncPath + @"\", StringComparison.OrdinalIgnoreCase), "The copy is outside the owned SMB share.");
                            progress = point.Bytes;
                            share.Control("Disconnect", manifestPath);
                            disconnected = true;
                        }));
                        result = await fixture.Operations().TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
                            mode, Scenarios.Replace, retainReplacedItems: true);
                    }
                    finally { share.Control("Reconnect", manifestPath); }
                    Fixture.Check(disconnected && progress > 0 && nativeError is not null && !result.Succeeded && !result.Cancelled &&
                        result.Transfers.Count == 0, "A real share interruption after native copy progress was not observed: " + string.Join("; ", result.Errors));
                    fixture.AssertOriginals();
                    var recovered = fixture.Operations().RecoverInterruptedTransfers();
                    Fixture.Check(recovered.Errors.Count == 0, string.Join("; ", recovered.Errors));
                    fixture.AssertOriginals();
                    fixture.AssertJournalEmpty();
                    fixture.AssertNoTemporaryItems();
                    var retry = await fixture.Operations().TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!, mode,
                        Scenarios.Replace, retainReplacedItems: true);
                    Fixture.Check(retry.Succeeded && retry.Transfers.Count == 1, "Reconnected SMB retry failed: " + string.Join("; ", retry.Errors));
                    fixture.AssertIncoming(fixture.State.Destination);
                    using (var history = await TransferHistorySession.CreateAsync(fixture.Operations(), retry.Transfers))
                    {
                        await history.UndoAsync();
                        fixture.AssertOriginals();
                    }
                    fixture.AssertJournalEmpty();
                    fixture.AssertNoTemporaryItems();
                    fixture.Cleanup();
                    results.Add(new(name, "DisposableLoopbackSmbDisconnection", "Passed", timer.Elapsed.TotalSeconds,
                        $"Removed only the owned SMB share after {progress} native copy bytes; native error {nativeError}. Originals/streams, recovery, retry and Undo passed. The test forces copy/delete for Move; it is not a remote-server or physical-network failure.", null));
                }
                catch (Exception ex)
                {
                    results.Add(new(name, "DisposableLoopbackSmbDisconnection", "Failed", timer.Elapsed.TotalSeconds, ex.ToString(),
                        fixture?.Root ?? Path.Combine(root, "fixtures", name)));
                }
                SaveReport();
                Console.WriteLine($"{results[^1].Status} {name}: {results[^1].Evidence}");
            }
        results.Add(await CommittedSourceOutage(root, share, manifestPath));
        SaveReport();
        Console.WriteLine($"{results[^1].Status} {results[^1].Name}: {results[^1].Evidence}");
        return results.Any(r => r.Status == "Failed") ? 1 : 0;
    }

    private static async Task<ScenarioResult> CommittedSourceOutage(string root, DisposableShare share, string manifestPath)
    {
        const string name = "smb-source-disconnect-after-replacement-commit";
        var timer = Stopwatch.StartNew();
        Fixture? fixture = null;
        try
        {
            fixture = Fixture.Create(root, name, true, secondVolumeRoot: share.UncPath);
            var operations = fixture.Operations();
            var prepare = await operations.TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
                FileTransferMode.Copy, Scenarios.Replace);
            Fixture.Check(prepare.Succeeded, "Could not prepare the generated SMB source.");
            fixture.AssertIncoming(fixture.State.Destination);
            var localParent = Directory.CreateDirectory(Path.Combine(fixture.Root, "committed-destination")).FullName;
            var destination = Path.Combine(localParent, Path.GetFileName(fixture.State.Destination));
            File.WriteAllText(destination, "old local destination");
            var disconnected = false;
            FileOperationResult moved;
            try
            {
                using var outage = new FileOperationFaultHooks.Scope(new(ForceCopyDelete: true, Checkpoint: point =>
                {
                    if (point.Phase != "ReplacementAfterHistoryCommitted" || disconnected) return;
                    Fixture.Check(point.Source == fixture.State.Destination, "Source outage escaped the owned fixture.");
                    share.Control("Disconnect", manifestPath);
                    disconnected = true;
                }));
                moved = await operations.TransferAsync([fixture.State.Destination], localParent, FileTransferMode.Move,
                    Scenarios.Replace, retainReplacedItems: true);
            }
            finally { share.Control("Reconnect", manifestPath); }
            Fixture.Check(disconnected && moved.Succeeded && moved.Transfers.Count == 1,
                "Source cleanup after commit rolled back or failed the completed Move: " + string.Join("; ", moved.Errors));
            fixture.AssertIncoming(destination);
            Fixture.Check(Directory.EnumerateFiles(fixture.Journal, "*.json").Any(), "Unavailable source cleanup lost its recovery journal.");
            var recovered = operations.RecoverInterruptedTransfers();
            Fixture.Check(recovered.Errors.Count == 0, string.Join("; ", recovered.Errors));
            fixture.AssertIncoming(destination);
            Fixture.Check(!LinkFileSystem.Exists(fixture.State.Destination), "The committed Move unexpectedly restored its source.");
            fixture.AssertJournalEmpty();
            fixture.AssertNoTemporaryItems();
            fixture.Cleanup();
            return new(name, "DisposableLoopbackSmbDisconnection", "Passed", timer.Elapsed.TotalSeconds,
                "Disconnected the owned source share after a replacement Move committed. Destination stayed complete; cleanup retained its journal and finished after reconnect without rolling back committed data.", null);
        }
        catch (Exception ex)
        {
            return new(name, "DisposableLoopbackSmbDisconnection", "Failed", timer.Elapsed.TotalSeconds, ex.ToString(),
                fixture?.Root ?? Path.Combine(root, "fixtures", name));
        }
    }
}
