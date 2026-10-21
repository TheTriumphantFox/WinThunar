using System.Diagnostics;
using System.Text.Json;
using WinThunar.Services;

namespace WinThunar.FileOps.Stress;

internal sealed record WorkerConfiguration(string Operation, string Phase, FileTransferMode Mode, bool ForceCopyDelete = false);

internal static class CrashWorker
{
    internal static async Task<int> RunAsync(string configPath)
    {
        var fixture = Fixture.Open(configPath);
        var configuration = JsonSerializer.Deserialize<WorkerConfiguration>(File.ReadAllText(configPath))
            ?? throw new InvalidDataException("Missing worker configuration.");
        var operations = fixture.Operations();
        TransferHistorySession? history = null;
        if (configuration.Operation == "undo")
        {
            using var setup = new FileOperationFaultHooks.Scope(new(ForceCopyDelete: configuration.ForceCopyDelete));
            var result = await operations.TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
                configuration.Mode, Scenarios.Replace, retainReplacedItems: true);
            Fixture.Check(result.Succeeded, string.Join("; ", result.Errors));
            history = await TransferHistorySession.CreateAsync(operations, result.Transfers);
        }
        using var faultScope = new FileOperationFaultHooks.Scope(new(ForceCopyDelete: configuration.ForceCopyDelete, Checkpoint: point =>
        {
            if (point.Phase != configuration.Phase || (point.Phase == "NativeCopyProgress" && (point.Bytes <= 0 || point.Bytes >= point.TotalBytes))) return;
            // Durable handshake at an actual operation boundary; the parent kills only this worker.
            using (var marker = new FileStream(fixture.MarkerPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                JsonSerializer.Serialize(marker, point, Fixture.JsonOptions);
                marker.Flush(flushToDisk: true);
            }
            Thread.Sleep(Timeout.Infinite);
        }));
        if (configuration.Operation == "undo") await history!.UndoAsync();
        else if (configuration.Operation == "recover") operations.RecoverInterruptedTransfers(skipActiveProcesses: true);
        else
        {
            var result = await operations.TransferAsync([fixture.State.Source], Path.GetDirectoryName(fixture.State.Destination)!,
                configuration.Mode, Scenarios.Replace, retainReplacedItems: true);
            Console.Error.WriteLine(JsonSerializer.Serialize(result));
        }
        throw new InvalidOperationException("The worker finished without reaching its requested fault checkpoint.");
    }

    internal static async Task<FileOperationFaultPoint> KillAtCheckpointAsync(Fixture fixture, WorkerConfiguration configuration)
    {
        File.WriteAllText(fixture.ConfigPath, JsonSerializer.Serialize(configuration, Fixture.JsonOptions));
        if (File.Exists(fixture.MarkerPath)) File.Delete(fixture.MarkerPath);
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing harness executable path.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = fixture.Root
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(CrashWorker).Assembly.Location);
        start.ArgumentList.Add("--worker");
        start.ArgumentList.Add(fixture.ConfigPath);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start test worker.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        try
        {
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(fixture.MarkerPath))
            {
                if (process.HasExited) throw new IOException($"Worker exited {process.ExitCode} before {configuration.Phase}: {await error} {await output}");
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException($"Worker did not reach {configuration.Phase} within 30 seconds.");
                await Task.Delay(25);
            }
            FileOperationFaultPoint? point = null;
            // The marker can be visible while its writer is still flushing; parse only a complete JSON document.
            while (point is null)
            {
                try { point = JsonSerializer.Deserialize<FileOperationFaultPoint>(File.ReadAllText(fixture.MarkerPath)); }
                catch (Exception ex) when (ex is IOException or JsonException) { }
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Incomplete checkpoint marker.");
                if (point is null) await Task.Delay(25);
            }
            var beforeKill = fixture.Operations().RecoverInterruptedTransfers(skipActiveProcesses: true);
            Fixture.Check(beforeKill.Errors.Count == 0 && beforeKill.RecoveredOperations == 0, "Recovery interfered with a live worker.");
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAll(error, output);
            return point;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }
}
