using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text.Json;

if (args is ["--verify-sources", var pdbPath, var manifestPath, var projectRoot])
{
    using var source = File.OpenRead(pdbPath);
    using var provider = MetadataReaderProvider.FromPortablePdbStream(source);
    var reader = provider.GetMetadataReader();
    var documents = reader.Documents.ToDictionary(handle => Path.GetFullPath(reader.GetString(reader.GetDocument(handle).Name)),
        handle => Convert.ToHexString(reader.GetBlobBytes(reader.GetDocument(handle).Hash)), StringComparer.OrdinalIgnoreCase);
    using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
    var checks = new List<object>();
    var sourcesPassed = true;
    foreach (var item in manifest.RootElement.GetProperty("sources").EnumerateArray())
    {
        var relative = item.GetProperty("path").GetString()!;
        var expected = item.GetProperty("sha256").GetString()!;
        var absolute = Path.GetFullPath(relative, projectRoot);
        var matchesCurrent = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(absolute))) == expected;
        var isCompiledCSharp = relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        var matchesCompiled = !isCompiledCSharp || (documents.TryGetValue(absolute, out var actual) && actual == expected);
        sourcesPassed &= matchesCurrent && matchesCompiled;
        checks.Add(new { path = relative, isCompiledCSharp, matchesCurrent, matchesCompiled });
    }
    Console.WriteLine(JsonSerializer.Serialize(new { passed = sourcesPassed, checks }, new JsonSerializerOptions { WriteIndented = true }));
    return sourcesPassed ? 0 : 1;
}

// Exercise the runtime's balanced COM-wrapper cache without WinUI or file I/O.
// Run against a generated WinThunar.runtimeconfig.json to verify the actual build policy.
var expectedDisabled = args.Contains("--expect-disabled");
var expectedDuplicates = args.Contains("--expect-duplicates");
if (expectedDisabled && expectedDuplicates) throw new ArgumentException("Choose one registry expectation.");
var wrappers = new ProbeComWrappers();
var target = new object();
var samples = new List<object>();
var clock = Stopwatch.StartNew();
const int callsPerSample = 500_000, sampleCount = 4;
var registry = typeof(ComWrappers).GetField("s_allManagedObjectWrapperTable", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
    ?? throw new InvalidOperationException("This runtime probe expects the .NET 10 diagnostic wrapper registry.");
var tryGetValue = registry.GetType().GetMethod("TryGetValue")!;
int RegistryCount()
{
    object?[] arguments = [target, null];
    return (bool)tryGetValue.Invoke(registry, arguments)! ? ((IList)arguments[1]!).Count : 0;
}
for (var sample = 1; sample <= sampleCount; sample++)
{
    for (var call = 0; call < callsPerSample; call++)
    {
        var pointer = wrappers.GetOrCreateComInterfaceForObject(target, CreateComInterfaceFlags.None);
        Marshal.Release(pointer);
    }
    // Probe diagnostics only; product code never forces collection.
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    samples.Add(new { calls = sample * callsPerSample, registryCount = RegistryCount(), managedBytes = GC.GetTotalMemory(false) });
}
var count = RegistryCount();
var passed = (!expectedDisabled || count == 0) && (!expectedDuplicates || count == callsPerSample * sampleCount);
Console.WriteLine(JsonSerializer.Serialize(new
{
    runtime = RuntimeInformation.FrameworkDescription,
    expectedDisabled, expectedDuplicates, calls = callsPerSample * sampleCount, registryCount = count,
    elapsedSeconds = clock.Elapsed.TotalSeconds, samples,
    passed
}, new JsonSerializerOptions { WriteIndented = true }));
GC.KeepAlive(target);
GC.KeepAlive(wrappers);
return passed ? 0 : 1;

sealed unsafe class ProbeComWrappers : ComWrappers
{
    protected override ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
    {
        count = 0;
        return null;
    }
    protected override object? CreateObject(nint externalComObject, CreateObjectFlags flags) => null;
    protected override void ReleaseObjects(IEnumerable objects) { }
}
