using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinThunar.Services;

/// <summary>
/// Registers WinThunar with the Windows shell under HKCU only, so nothing needs elevation and every
/// change can be reverted. Prior values are recorded before they are replaced.
/// </summary>
public sealed class ShellRegistrationService
{
    public const string VerbName = "WinThunar";
    public const string RegisterSwitch = "--register-shell";
    public const string RegisterDefaultSwitch = "--register-default";
    public const string UnregisterSwitch = "--unregister-shell";

    private const string Absent = "\0absent";

    private static readonly string[] VerbParents = ["Directory", @"Directory\Background", "Drive"];
    private static readonly string[] DefaultVerbParents = ["Directory", "Drive"];

    private readonly RegistryKey _root;
    private readonly string _classesPath;
    private readonly string _statePath;
    private readonly string _executablePath;

    public ShellRegistrationService(
        string executablePath,
        RegistryKey? root = null,
        string classesPath = @"Software\Classes",
        string statePath = @"Software\WinThunar\ShellIntegration")
    {
        _executablePath = executablePath;
        _root = root ?? Registry.CurrentUser;
        _classesPath = classesPath;
        _statePath = statePath;
    }

    /// <summary>
    /// MSIX-packaged processes write HKCU into a private hive the shell never reads, so registration
    /// only works from the portable build.
    /// </summary>
    public static bool IsSupported
    {
        get
        {
            var length = 0;
            return GetCurrentPackageFullName(ref length, null) == AppModelErrorNoPackage;
        }
    }

    public bool IsOpenVerbRegistered => VerbParents.All(parent => ReadCommand(parent) is not null);

    public bool IsDefaultFileManager
    {
        get
        {
            using var state = _root.OpenSubKey(_statePath);
            return state?.GetValue("DefaultFileManager") is 1;
        }
    }

    /// <summary>The executable the registered commands launch, if any.</summary>
    public string? RegisteredExecutable =>
        ReadCommand("Directory") is { } command ? LaunchRequest.SplitCommandLine(command).FirstOrDefault() : null;

    public void SetOpenVerb(bool enabled)
    {
        if (!enabled)
        {
            SetDefaultFileManager(false);
            foreach (var parent in VerbParents)
            {
                _root.DeleteSubKeyTree(ClassPath(parent, "shell", VerbName), false);
                PruneEmptyKeys(ClassPath(parent, "shell"));
            }

            NotifyShell();
            return;
        }

        foreach (var parent in VerbParents)
        {
            using var verb = _root.CreateSubKey(ClassPath(parent, "shell", VerbName));
            verb.SetValue("MUIVerb", "Open in WinThunar");
            verb.SetValue("Icon", $"\"{_executablePath}\",0");
            using var command = verb.CreateSubKey("command");
            command.SetValue(string.Empty, $"\"{_executablePath}\" \"%V\"");
        }

        NotifyShell();
    }

    public void SetDefaultFileManager(bool enabled)
    {
        if (enabled == IsDefaultFileManager)
        {
            return;
        }

        if (enabled)
        {
            SetOpenVerb(true);
            using var state = _root.CreateSubKey(_statePath);
            foreach (var parent in DefaultVerbParents)
            {
                using var shell = _root.CreateSubKey(ClassPath(parent, "shell"));
                state.SetValue($"{parent}.shell", shell.GetValue(string.Empty) as string ?? Absent);
                shell.SetValue(string.Empty, VerbName);
            }

            state.SetValue("DefaultFileManager", 1);
        }
        else
        {
            using var state = _root.OpenSubKey(_statePath, true);
            foreach (var parent in DefaultVerbParents)
            {
                using var shell = _root.OpenSubKey(ClassPath(parent, "shell"), true);
                if (shell is null)
                {
                    continue;
                }

                var previous = state?.GetValue($"{parent}.shell") as string ?? Absent;
                // Leave the value alone if something else has since claimed the default.
                if (string.Equals(shell.GetValue(string.Empty) as string, VerbName, StringComparison.Ordinal))
                {
                    RestoreValue(shell, string.Empty, previous);
                }

                shell.Dispose();
                PruneEmptyKeys(ClassPath(parent, "shell"));
            }

            if (state is not null)
            {
                foreach (var name in state.GetValueNames())
                {
                    state.DeleteValue(name, false);
                }
            }
        }

        NotifyShell();
    }

    /// <summary>
    /// Points registered commands at this executable when the previously registered one no longer
    /// exists (the portable folder moved). Returns true when something was rewritten.
    /// </summary>
    public bool RepairExecutablePath()
    {
        if (RegisteredExecutable is not { } registered ||
            string.Equals(registered, _executablePath, StringComparison.OrdinalIgnoreCase) ||
            File.Exists(registered))
        {
            return false;
        }

        SetOpenVerb(true);
        NotifyShell();
        return true;
    }

    /// <summary>
    /// Deletes a key and then its parents while they are empty, stopping at the classes root. An empty
    /// HKCU class key can otherwise mask the machine-wide one in the merged HKCR view.
    /// </summary>
    private void PruneEmptyKeys(string path)
    {
        while (path.Length > _classesPath.Length &&
            path.StartsWith(_classesPath + "\\", StringComparison.OrdinalIgnoreCase))
        {
            using (var key = _root.OpenSubKey(path))
            {
                if (key is null)
                {
                    path = path[..path.LastIndexOf('\\')];
                    continue;
                }

                if (key.ValueCount > 0 || key.SubKeyCount > 0)
                {
                    return;
                }
            }

            _root.DeleteSubKey(path, false);
            path = path[..path.LastIndexOf('\\')];
        }
    }

    private static void RestoreValue(RegistryKey key, string name, string previous)
    {
        if (previous == Absent)
        {
            key.DeleteValue(name, false);
        }
        else
        {
            key.SetValue(name, previous);
        }
    }

    private string? ReadCommand(string parent)
    {
        using var command = _root.OpenSubKey(ClassPath(parent, "shell", VerbName, "command"));
        return command?.GetValue(string.Empty) as string;
    }

    private string ClassPath(params string[] parts) => string.Join('\\', [_classesPath, .. parts]);

    private static void NotifyShell()
    {
        try
        {
            SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
        }
        catch (DllNotFoundException)
        {
        }
    }

    private const int AppModelErrorNoPackage = 15700;
    private const int ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
