using System.Text.Json;
using WinThunar.Services;

namespace WinThunar.FileOps.Stress;

internal sealed record FixtureState(string RunId, string Root, string? ExternalRoot, string Source, string Destination,
    string PayloadSource, string PayloadDestination, PathStateSnapshot SourceBefore, PathStateSnapshot? DestinationBefore,
    PathStateSnapshot PayloadBefore, PathStateSnapshot? OldPayload);

internal sealed class Fixture
{
    internal const int PayloadBytes = 8 * 1024 * 1024;
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal FixtureState State { get; }
    internal string Root => State.Root;
    internal string Journal => Path.Combine(Root, "journal");
    internal string ConfigPath => Path.Combine(Root, "worker.json");
    internal string MarkerPath => Path.Combine(Root, "checkpoint.json");
    internal FileOperationService Operations() => new(new TransferRecoveryService(Journal));

    private Fixture(FixtureState state) => State = state;

    internal static Fixture Create(string runRoot, string name, bool replace, bool merge = false, string? secondVolumeRoot = null, bool alternateStreams = true,
        bool largeAlternateStream = false)
    {
        var runId = File.ReadAllText(Path.Combine(runRoot, "run-id.txt")).Trim();
        var root = Path.GetFullPath(Path.Combine(runRoot, "fixtures", name));
        Check(!Directory.Exists(root), "Fixture directory must be new.");
        Directory.CreateDirectory(root);
        var sourceParent = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        string? externalRoot = null;
        if (secondVolumeRoot is not null)
        {
            Check(Directory.Exists(secondVolumeRoot), "The supplied second-volume directory does not exist.");
            externalRoot = Path.Combine(Path.GetFullPath(secondVolumeRoot), "WinThunar-Stress-" + runId + "-" + name);
            Check(!Directory.Exists(externalRoot), "External fixture directory must be new.");
            Directory.CreateDirectory(externalRoot);
            File.WriteAllText(Path.Combine(externalRoot, "fixture-owner.txt"), runId);
        }
        var destinationParent = Directory.CreateDirectory(Path.Combine(externalRoot ?? root, "destination")).FullName;
        var source = merge ? Directory.CreateDirectory(Path.Combine(sourceParent, "tree")).FullName : Path.Combine(sourceParent, "item.bin");
        var destination = Path.Combine(destinationParent, merge ? "tree" : "item.bin");
        var payloadSource = source;
        var payloadDestination = destination;
        if (merge)
        {
            payloadSource = Path.Combine(Directory.CreateDirectory(Path.Combine(source, "nested")).FullName, "item.bin");
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            File.WriteAllText(Path.Combine(source, "new.txt"), "new synthetic file");
            payloadDestination = Path.Combine(destination, "nested", "item.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(payloadDestination)!);
            File.WriteAllText(Path.Combine(destination, "keep.txt"), "unrelated destination item");
        }
        WritePayload(payloadSource, alternateStreams);
        if (largeAlternateStream)
        {
            Check(alternateStreams, "A partial-write fixture requires an alternate stream.");
            using (var stream = new FileStream(payloadSource + ":WinThunar.Stress", FileMode.Create, FileAccess.Write))
            {
                var block = new byte[128 * 1024];
                new Random(71823).NextBytes(block);
                for (var offset = 0; offset < PayloadBytes; offset += block.Length) stream.Write(block);
            }
            File.SetLastWriteTimeUtc(payloadSource, new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        }
        if (replace)
        {
            File.WriteAllText(payloadDestination, "old destination content");
            if (alternateStreams) File.WriteAllText(payloadDestination + ":WinThunar.Stress", "old destination stream");
        }
        if (!alternateStreams && merge)
        {
            // FAT timestamps have coarser precision. Use representable fixture values rather than
            // treating round-trip loss of NTFS timestamp precision as loss of file contents.
            var timestamp = new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) File.SetLastWriteTimeUtc(file, timestamp);
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                Directory.SetLastWriteTimeUtc(directory, timestamp);
            Directory.SetLastWriteTimeUtc(source, timestamp);
        }
        var state = new FixtureState(runId, root, externalRoot, source, destination, payloadSource, payloadDestination,
            FileOperationService.CapturePathState(source), LinkFileSystem.Exists(destination) ? FileOperationService.CapturePathState(destination) : null,
            FileOperationService.CapturePathState(payloadSource), File.Exists(payloadDestination) ? FileOperationService.CapturePathState(payloadDestination) : null);
        File.WriteAllText(Path.Combine(root, "fixture.json"), JsonSerializer.Serialize(state, JsonOptions));
        return new Fixture(state);
    }

    internal static Fixture Open(string configPath)
    {
        var root = Path.GetFullPath(Path.GetDirectoryName(configPath)!);
        var state = JsonSerializer.Deserialize<FixtureState>(File.ReadAllText(Path.Combine(root, "fixture.json")))
            ?? throw new InvalidDataException("Missing fixture ownership metadata.");
        Check(string.Equals(root, state.Root, StringComparison.OrdinalIgnoreCase), "Fixture metadata names a different root.");
        var runRoot = Path.GetDirectoryName(Path.GetDirectoryName(root)!)!;
        Check(Path.GetFileName(Path.GetDirectoryName(root)) == "fixtures" &&
            File.ReadAllText(Path.Combine(runRoot, "run-id.txt")).Trim() == state.RunId, "Fixture is outside its generated run directory.");
        Check(!LinkFileSystem.IsReparsePoint(root), "Fixture root cannot be a link.");
        foreach (var path in new[] { state.Source, state.PayloadSource }) Check(Within(path, root), "Source escaped its fixture.");
        foreach (var path in new[] { state.Destination, state.PayloadDestination })
            Check(Within(path, state.ExternalRoot ?? root), "Destination escaped its fixture.");
        if (state.ExternalRoot is { } external)
            Check(File.ReadAllText(Path.Combine(external, "fixture-owner.txt")) == state.RunId &&
                Path.GetFileName(external).StartsWith("WinThunar-Stress-" + state.RunId + "-", StringComparison.Ordinal), "External fixture ownership mismatch.");
        return new Fixture(state);
    }

    private static bool Within(string path, string parent) => Path.GetFullPath(path)
        .StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void WritePayload(string path, bool alternateStreams)
    {
        var bytes = new byte[128 * 1024];
        new Random(74391).NextBytes(bytes);
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            for (var offset = 0; offset < PayloadBytes; offset += bytes.Length) file.Write(bytes);
        if (alternateStreams) File.WriteAllText(path + ":WinThunar.Stress", "incoming alternate-stream fixture");
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc));
    }

    internal void AssertOriginals()
    {
        Check(FileOperationService.PathMatchesState(State.Source, State.SourceBefore), "Source data/metadata/streams changed after a failed transfer.");
        AssertState(State.Destination, State.DestinationBefore, "Existing destination changed after a failed transfer.");
    }

    internal void AssertIncoming(string path) => Check(FileOperationService.PathMatchesState(path, State.PayloadBefore),
        $"Incoming payload/metadata/alternate streams do not match at '{path}'.");

    internal void AssertOldPayload() => AssertState(State.PayloadDestination, State.OldPayload, "Old destination data or streams were not restored.");

    internal static void AssertState(string path, PathStateSnapshot? snapshot, string message)
        => Check(snapshot is null ? !LinkFileSystem.Exists(path) : FileOperationService.PathMatchesState(path, snapshot), message);

    internal void AssertJournalEmpty() => Check(!Directory.Exists(Journal) || !Directory.EnumerateFiles(Journal).Any(), "Recovery records were left behind.");

    internal void AssertNoTemporaryItems()
    {
        foreach (var parent in new[] { Root, State.ExternalRoot }.Where(path => path is not null))
            Check(!Directory.EnumerateFileSystemEntries(parent!, ".winthunar-*", SearchOption.AllDirectories).Any(), "Private staging/history data was left behind.");
    }

    internal void Cleanup()
    {
        // Re-open and validate all ownership/containment checks immediately before deletion.
        _ = Open(ConfigPath);
        if (State.ExternalRoot is { } external)
        {
            Check(!LinkFileSystem.IsReparsePoint(external), "External cleanup root cannot be a link.");
            LinkFileSystem.Delete(external);
        }
        LinkFileSystem.Delete(Root);
    }

    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
