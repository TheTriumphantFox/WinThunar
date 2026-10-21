using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using WinThunar.Services;

namespace WinThunar.FileOps.Stress;

internal sealed record ScenarioResult(string Name, string Kind, string Status, double Seconds, string Evidence, string? FixturePath);

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["--worker", var config]) return await CrashWorker.RunAsync(config);
            if (args is ["--smb-run", var shareManifest, var smbOutput]) return await SmbScenarios.RunAsync(shareManifest, smbOutput);
            if (args is ["--search-performance", var performanceOutput]) return SearchPerformance.Run(performanceOutput);
            if (args.Length < 2 || args[0] != "--run") throw new ArgumentException("Use --run NEW_OUTPUT_DIRECTORY [--second-volume DIRECTORY [--basic-second-volume] | --disposable-volume-manifest FILE] [--keep-fixtures].");
            var root = Path.GetFullPath(args[1]);
            string? secondVolume = null;
            var keepFixtures = false;
            var basicVolume = false;
            string? disposableManifest = null;
            for (var index = 2; index < args.Length; index++)
            {
                if (args[index] == "--keep-fixtures") keepFixtures = true;
                else if (args[index] == "--basic-second-volume") basicVolume = true;
                else if (args[index] == "--second-volume" && index + 1 < args.Length) secondVolume = Path.GetFullPath(args[++index]);
                else if (args[index] == "--disposable-volume-manifest" && index + 1 < args.Length) disposableManifest = Path.GetFullPath(args[++index]);
                else throw new ArgumentException("Unknown stress-harness argument: " + args[index]);
            }
            Fixture.Check(OperatingSystem.IsWindows(), "This harness requires Windows native copy APIs and NTFS alternate streams.");
            Fixture.Check(!basicVolume || secondVolume is not null, "Basic volume coverage requires --second-volume.");
            var disposableVolume = disposableManifest is null ? null : DisposableVolume.Open(disposableManifest);
            Fixture.Check(disposableVolume is null || (secondVolume is null && !basicVolume), "Disposable-volume mode supplies its own NTFS second volume.");
            if (disposableVolume is not null) secondVolume = disposableVolume.MountPath;
            Fixture.Check(!Directory.Exists(root) && !File.Exists(root), "Choose a new output directory.");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "run-id.txt"), Guid.NewGuid().ToString("N"));
            var started = DateTimeOffset.UtcNow;
            var results = new List<ScenarioResult>();
            void SaveReport() => File.WriteAllText(Path.Combine(root, "report.json"), JsonSerializer.Serialize(new
            {
                startedUtc = started, updatedUtc = DateTimeOffset.UtcNow,
                framework = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                passed = results.Count(result => result.Status == "Passed"), failed = results.Count(result => result.Status == "Failed"),
                skipped = results.Count(result => result.Status == "Skipped"), scenarios = results,
                secondVolumeRoot = secondVolume, basicSecondVolume = basicVolume,
                disposableVolumeManifest = disposableManifest,
                limits = new[] { "NativeErrorInjection cases simulate storage errors. Real disk-full cases run only on an explicitly provisioned, bounded disposable volume.",
                    "Copy/delete fallback on one volume is separate from optional real distinct-volume coverage.",
                    "Owned worker termination tests process crashes, not power loss. Multi-item history is not crash-atomic." }
            }, Fixture.JsonOptions));

            async Task Run(string name, string kind, bool replace, Func<Fixture, Task<string>> scenario, bool merge = false, bool external = false, bool alternateStreams = true,
                bool largeAlternateStream = false)
            {
                var timer = Stopwatch.StartNew();
                Fixture? fixture = null;
                try
                {
                    fixture = Fixture.Create(root, name, replace, merge, external ? secondVolume : null, alternateStreams, largeAlternateStream);
                    var evidence = await scenario(fixture);
                    if (!keepFixtures) fixture.Cleanup();
                    results.Add(new(name, kind, "Passed", timer.Elapsed.TotalSeconds, evidence, keepFixtures ? fixture.Root : null));
                    Console.WriteLine($"PASS {name}: {evidence}");
                }
                catch (Exception ex)
                {
                    // Fixture construction can fail after creating data on either volume. Keep
                    // the local path discoverable even when no complete Fixture was returned.
                    results.Add(new(name, kind, "Failed", timer.Elapsed.TotalSeconds, ex.ToString(), fixture?.Root ?? Path.Combine(root, "fixtures", name)));
                    Console.WriteLine($"FAIL {name}: {ex.Message}");
                }
                SaveReport();
            }
            void Skip(string name, string evidence)
            {
                results.Add(new(name, "RealStorage", "Skipped", 0, evidence, null));
                Console.WriteLine($"SKIP {name}: {evidence}");
                SaveReport();
            }

            foreach (var (label, code) in new[] { ("disk-full", 112), ("share-disconnected", 64), ("device-removed", 1167) })
                foreach (var mode in new[] { FileTransferMode.Copy, FileTransferMode.Move })
                    foreach (var replace in new[] { false, true })
                        await Run($"{label}-{mode.ToString().ToLowerInvariant()}-{(replace ? "replace" : "new")}", "NativeErrorInjection", replace,
                            fixture => Scenarios.NativeFailure(fixture, code, mode));
            await Run("cancel-before-copy", "Cancellation", true, fixture => Scenarios.Cancellation(fixture, true));
            await Run("cancel-during-copy", "Cancellation", true, fixture => Scenarios.Cancellation(fixture, false));
            await Run("locked-cleanup-and-recovery-outage", "NativeErrorInjectionAndRealSharingLock", false, Scenarios.LockedCleanupAndUnavailableRecovery);
            await Run("recovery-keeps-live-journal-responsive", "RecoveryConcurrency", false, Scenarios.RecoveryDoesNotBlockLiveJournalWrites);
            await Run("copy-delete-file-history", "ForcedCopyDelete", true, fixture => Scenarios.HistoryRoundTrip(fixture, true, false));
            await Run("copy-delete-merge-history", "ForcedCopyDelete", true, fixture => Scenarios.HistoryRoundTrip(fixture, true, false), merge: true);

            foreach (var phase in new[] { "NativeCopyProgress", "ReplacementAfterBackup", "ReplacementAfterPromotion", "ReplacementAfterSourceStaged", "FileAfterPromotion" })
                await Run("kill-" + phase.ToLowerInvariant(), "OwnedProcessTermination", phase != "FileAfterPromotion",
                    fixture => Scenarios.KilledTransfer(fixture, phase, phase == "ReplacementAfterSourceStaged" || phase == "FileAfterPromotion" ? FileTransferMode.Move : FileTransferMode.Copy));
            await Run("kill-nested-merge", "OwnedProcessTermination", true,
                fixture => Scenarios.KilledTransfer(fixture, "ReplacementAfterBackup", FileTransferMode.Copy), merge: true);
            await Run("kill-recovery-and-retry", "OwnedProcessTermination", true, Scenarios.KilledRecovery);
            foreach (var phase in new[] { "HistoryAfterSourceStaged", "HistoryAfterPromotion" })
                foreach (var originalFirst in new[] { true, false })
                    await Run($"kill-{phase.ToLowerInvariant()}-{(originalFirst ? "original" : "replay")}-first", "OwnedProcessTermination", true,
                        fixture => Scenarios.KilledHistory(fixture, phase, originalFirst));
            await Run("kill-copy-undo-before-restore", "OwnedProcessTermination", true,
                fixture => Scenarios.KilledHistory(fixture, "HistoryAfterIncomingParked", true));

            if (basicVolume)
            {
                await Run("real-cross-volume-basic-file-history", "RealStorageWithoutAlternateStreams", true,
                    fixture => Scenarios.HistoryRoundTrip(fixture, false, true, false), external: true, alternateStreams: false);
                await Run("real-cross-volume-basic-merge-history", "RealStorageWithoutAlternateStreams", true,
                    fixture => Scenarios.HistoryRoundTrip(fixture, false, true, false), merge: true, external: true, alternateStreams: false);
                Skip("real-cross-volume-file-history", "Basic non-NTFS volume selected; NTFS alternate-stream cross-volume coverage remains unverified.");
                Skip("real-cross-volume-merge-history", "Basic non-NTFS volume selected; full NTFS metadata/stream coverage remains unverified.");
            }
            else if (secondVolume is null)
            {
                Skip("real-cross-volume-file-history", "Supply --second-volume on a distinct writable NTFS volume to exercise real cross-volume Move/Undo/Redo.");
                Skip("real-cross-volume-merge-history", "No second volume supplied; forced fallback coverage is reported separately.");
            }
            else
            {
                await Run("real-cross-volume-file-history", "RealStorage", true, fixture => Scenarios.HistoryRoundTrip(fixture, false, true), external: true);
                await Run("real-cross-volume-merge-history", "RealStorage", true, fixture => Scenarios.HistoryRoundTrip(fixture, false, true), merge: true, external: true);
            }
            if (disposableVolume is null)
                Skip("physical-disk-full", "Use Test-FileOperationsRealStorage.ps1 to provision a bounded disposable VHD for real disk-full checks.");
            else
            {
                foreach (var mode in new[] { FileTransferMode.Copy, FileTransferMode.Move })
                    foreach (var replace in new[] { false, true })
                        await Run($"real-disk-full-{mode.ToString().ToLowerInvariant()}-{(replace ? "replace" : "new")}", "RealVirtualDiskFull", replace,
                            fixture => Scenarios.RealDiskFull(fixture, disposableVolume, mode), external: true);
                foreach (var mode in new[] { FileTransferMode.Copy, FileTransferMode.Move })
                    foreach (var replace in new[] { false, true })
                        await Run($"real-disk-full-after-main-stream-{mode.ToString().ToLowerInvariant()}-{(replace ? "replace" : "new")}", "RealVirtualDiskFullAfterProgress", replace,
                            fixture => Scenarios.RealDiskFull(fixture, disposableVolume, mode, requireProgress: true), external: true, largeAlternateStream: true);
            }
            Skip("physical-share-disconnection", "Use Test-FileOperationsSmb.ps1 for owned loopback-share interruption. Remote-server/network interruption remains unverified; no user shares are disconnected.");
            Skip("physical-device-removal", "No devices are detached; physical removal remains a manual check.");
            Console.WriteLine($"Report: {Path.Combine(root, "report.json")}");
            Console.WriteLine($"Passed {results.Count(result => result.Status == "Passed")}; failed {results.Count(result => result.Status == "Failed")}; skipped {results.Count(result => result.Status == "Skipped")}.");
            return results.Any(result => result.Status == "Failed") ? 1 : 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 2; }
    }
}
