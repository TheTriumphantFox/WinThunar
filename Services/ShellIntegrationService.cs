using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WinThunar.Services;

public static class ShellIntegrationService
{
    private const uint SeeMaskInvokeIdList = 0x0000000C;

    public static bool IsShortcutFile(string path) =>
        Path.GetExtension(path) is { } extension &&
        (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".url", StringComparison.OrdinalIgnoreCase));

    public static bool TryResolveShortcutTarget(string shortcutPath, out string targetPath)
    {
        targetPath = string.Empty;
        if (!Path.GetExtension(shortcutPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        object? shellObject = null;
        object? shortcutObject = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            shellObject = shellType is null ? null : Activator.CreateInstance(shellType);
            if (shellObject is null)
            {
                return false;
            }

            dynamic shell = shellObject;
            shortcutObject = shell.CreateShortcut(Path.GetFullPath(shortcutPath));
            dynamic shortcut = shortcutObject;
            var candidate = shortcut.TargetPath as string;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            targetPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate));
            return true;
        }
        catch
        {
            targetPath = string.Empty;
            return false;
        }
        finally
        {
            foreach (var value in new[] { shortcutObject, shellObject })
            {
                if (value is not null && Marshal.IsComObject(value))
                {
                    Marshal.FinalReleaseComObject(value);
                }
            }
        }
    }

    public static bool OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool ShowProperties(string path, nint ownerWindow)
    {
        var info = new ShellExecuteInfo
        {
            cbSize = Marshal.SizeOf<ShellExecuteInfo>(),
            fMask = SeeMaskInvokeIdList,
            hwnd = ownerWindow,
            lpVerb = "properties",
            lpFile = path,
            nShow = 1
        };

        return ShellExecuteEx(ref info);
    }

    public static void OpenShellLocation(string shellLocation)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = shellLocation,
            UseShellExecute = true
        });
    }

    public static int EmptyRecycleBin(nint ownerWindow) =>
        SHEmptyRecycleBin(ownerWindow, null, 0x00000001 | 0x00000002 | 0x00000004);

    public static void OpenTerminal(string workingDirectory)
    {
        try
        {
            var terminal = new ProcessStartInfo { FileName = "wt.exe", UseShellExecute = true };
            terminal.ArgumentList.Add("-d");
            terminal.ArgumentList.Add(workingDirectory);
            Process.Start(terminal);
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = workingDirectory,
                UseShellExecute = true
            });
        }
    }

    public static bool EjectDrive(string driveRoot)
    {
        object? shellObject = null;
        object? folderObject = null;
        object? itemObject = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            shellObject = shellType is null ? null : Activator.CreateInstance(shellType);
            if (shellObject is null)
            {
                return false;
            }

            dynamic shell = shellObject;
            folderObject = shell.NameSpace(17);
            if (folderObject is null)
            {
                return false;
            }

            dynamic folder = folderObject;
            itemObject = folder.ParseName(driveRoot.TrimEnd('\\'));
            if (itemObject is null)
            {
                return false;
            }

            dynamic item = itemObject;
            item.InvokeVerb("Eject");
            return true;
        }
        finally
        {
            foreach (var value in new[] { itemObject, folderObject, shellObject })
            {
                if (value is not null && Marshal.IsComObject(value))
                {
                    Marshal.FinalReleaseComObject(value);
                }
            }
        }
    }

    [DllImport("shell32.dll", EntryPoint = "ShellExecuteExW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo executeInfo);

    [DllImport("shell32.dll", EntryPoint = "SHEmptyRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(nint hwnd, string? rootPath, uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int cbSize;
        public uint fMask;
        public nint hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectory;
        public int nShow;
        public nint hInstApp;
        public nint lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpClass;
        public nint hkeyClass;
        public uint dwHotKey;
        public nint hIconOrMonitor;
        public nint hProcess;
    }
}
