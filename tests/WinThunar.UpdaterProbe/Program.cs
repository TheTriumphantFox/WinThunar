using System.Text.Json;
using System.Xml.Linq;
using Velopack;
using Velopack.Locators;
using WinThunar.Services;

// A separate console executable; never launch the real file manager or its installed updater.
var caseRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("WINTHUNAR_UPDATER_PROBE_CASE")
    ?? throw new InvalidOperationException("An owned probe case is required."));
var token = Environment.GetEnvironmentVariable("WINTHUNAR_UPDATER_PROBE_TOKEN");
var owner = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseRoot, "owner.json"))).RootElement;
var appId = owner.GetProperty("AppId").GetString()!;
if (string.IsNullOrEmpty(token) || owner.GetProperty("Token").GetString() != token ||
    !appId.StartsWith("WinThunar.UpdaterProbe.", StringComparison.Ordinal) ||
    !Guid.TryParseExact(appId["WinThunar.UpdaterProbe.".Length..], "N", out _))
    throw new InvalidOperationException("Probe ownership does not match.");
var installRoot = Path.Combine(caseRoot, "installation");
if (!Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory).Equals(
        Path.Combine(installRoot, "current"), StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("The probe must run inside its owned installation.");
// Refuse reparse points before handing any path to the real updater.
for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
        throw new InvalidOperationException("Probe paths cannot contain links or mount points.");
var manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "sq.version"));
if (manifest.Descendants().Single(e => e.Name.LocalName == "id").Value != appId)
    throw new InvalidOperationException("The package identity does not match the probe.");

VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
var locator = VelopackLocator.Current;
if (locator.AppId != appId || !Path.GetFullPath(locator.RootAppDir!).Equals(installRoot, StringComparison.OrdinalIgnoreCase) ||
    !Path.GetFullPath(locator.PackagesDir!).Equals(Path.Combine(installRoot, "packages"), StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Velopack escaped the owned installation.");
Environment.SetEnvironmentVariable(UpdateService.FeedOverrideVariable, Path.Combine(caseRoot, "feed"));
var updates = new UpdateService(new UpdateSettingsStore(Path.Combine(caseRoot, "updates.json")));
var initialState = updates.State;
if (args is ["status"] or ["status-after-apply"])
{
    if (args[0] == "status-after-apply") await updates.CheckAsync();
    WriteReport(args[0] + ".json");
    return 0;
}
if (args is not ["download"] and not ["apply-on-exit"])
    throw new ArgumentException("Expected status, download, or apply-on-exit.");
await updates.CheckAsync();
WriteReport(args[0] == "download" ? "download.json" : "pending-before-close.json");
if (updates.State != UpdateState.Ready) return 1;
if (args[0] == "download") return 0;

// The parent inspects Ready while this process stays alive, then releases the close gate.
File.WriteAllText(Path.Combine(caseRoot, "ready"), Environment.ProcessId.ToString());
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
while (!File.Exists(Path.Combine(caseRoot, "close")))
    await Task.Delay(100, deadline.Token);
updates.ApplyOnExit();
WriteReport("scheduled.json");
return 0;

void WriteReport(string file) => File.WriteAllText(Path.Combine(caseRoot, file), JsonSerializer.Serialize(new
{
    ProcessId = Environment.ProcessId,
    AppId = appId,
    InitialState = initialState.ToString(),
    State = updates.State.ToString(),
    updates.CurrentVersion,
    updates.AvailableVersion,
    updates.RequiresAdministrator,
    updates.ErrorMessage,
    RootAppDir = locator.RootAppDir,
    PackagesDir = locator.PackagesDir,
    CapturedAt = DateTimeOffset.UtcNow
}, new JsonSerializerOptions { WriteIndented = true }));
