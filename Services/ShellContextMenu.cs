using System.Runtime.InteropServices;

namespace WinThunar.Services;

/// <summary>
/// Hosts the Windows shell's own context menu (the "Show more options" menu) for items or a folder
/// background, so third-party shell extensions, Send To, and Open With appear as they do in Explorer.
/// </summary>
public static class ShellContextMenu
{
    private const uint FirstCommandId = 1;
    private const uint LastCommandId = 0x7FFF;

    // Kept alive for the lifetime of the process; the native subclass holds a pointer to it.
    private static readonly SubclassProc MenuMessageForwarder = ForwardMenuMessage;
    private static IContextMenu2? s_activeMenu2;
    private static IContextMenu3? s_activeMenu3;

    /// <summary>
    /// Shows the shell menu at a screen point and runs the chosen command. Items must share one parent
    /// folder; pass none for the folder background menu. Returns the invoked verb, or null if cancelled.
    /// </summary>
    public static string? Show(nint ownerWindow, string folder, IReadOnlyList<string> items, int screenX, int screenY, bool extendedVerbs)
    {
        var pidls = new List<IntPtr>();
        object? folderObject = null;
        object? menuObject = null;
        var menu = IntPtr.Zero;
        var subclassed = false;
        try
        {
            var menuGuid = typeof(IContextMenu).GUID;
            if (items.Count == 0)
            {
                var folderPidl = ParseDisplayName(folder);
                pidls.Add(folderPidl);
                var shellFolderGuid = typeof(IShellFolder).GUID;
                Marshal.ThrowExceptionForHR(SHBindToObject(IntPtr.Zero, folderPidl, IntPtr.Zero, ref shellFolderGuid, out folderObject));
                ((IShellFolder)folderObject).CreateViewObject(ownerWindow, ref menuGuid, out menuObject);
            }
            else
            {
                var children = new IntPtr[items.Count];
                for (var index = 0; index < items.Count; index++)
                {
                    var absolute = ParseDisplayName(items[index]);
                    pidls.Add(absolute);
                    var shellFolderGuid = typeof(IShellFolder).GUID;
                    Marshal.ThrowExceptionForHR(SHBindToParent(absolute, ref shellFolderGuid, out var parent, out children[index]));
                    if (folderObject is null)
                    {
                        folderObject = parent;
                    }
                    else
                    {
                        Marshal.ReleaseComObject(parent);
                    }
                }

                ((IShellFolder)folderObject!).GetUIObjectOf(ownerWindow, (uint)children.Length, children, ref menuGuid, IntPtr.Zero, out menuObject);
            }

            var contextMenu = (IContextMenu)menuObject;
            menu = CreatePopupMenu();
            var flags = ContextMenuFlags.Normal | ContextMenuFlags.Explore;
            if (extendedVerbs)
            {
                flags |= ContextMenuFlags.ExtendedVerbs;
            }

            // Success codes carry the number of commands added, so only negative results are failures.
            Marshal.ThrowExceptionForHR(contextMenu.QueryContextMenu(menu, 0, FirstCommandId, LastCommandId, flags));

            s_activeMenu3 = menuObject as IContextMenu3;
            s_activeMenu2 = s_activeMenu3 is null ? menuObject as IContextMenu2 : null;
            subclassed = SetWindowSubclass(ownerWindow, MenuMessageForwarder, 1, IntPtr.Zero);

            // Without foreground, the menu does not dismiss when the user clicks elsewhere.
            SetForegroundWindow(ownerWindow);
            var command = TrackPopupMenuEx(menu, TrackPopupFlags.ReturnCommand | TrackPopupFlags.RightButton, screenX, screenY, ownerWindow, IntPtr.Zero);
            if (command < FirstCommandId)
            {
                return null;
            }

            var offset = (IntPtr)(command - FirstCommandId);
            var verb = GetVerb(contextMenu, offset);
            var invoke = new CommandInvokeInfo
            {
                cbSize = Marshal.SizeOf<CommandInvokeInfo>(),
                fMask = InvokeMask.Unicode | InvokeMask.PointInvoke | ModifierMask(),
                hwnd = ownerWindow,
                lpVerb = offset,
                lpVerbW = offset,
                lpDirectoryW = folder,
                nShow = 1,
                ptInvoke = new Point { X = screenX, Y = screenY }
            };
            Marshal.ThrowExceptionForHR(contextMenu.InvokeCommand(ref invoke));
            return verb ?? string.Empty;
        }
        finally
        {
            if (subclassed)
            {
                RemoveWindowSubclass(ownerWindow, MenuMessageForwarder, 1);
            }

            s_activeMenu2 = null;
            s_activeMenu3 = null;
            if (menu != IntPtr.Zero)
            {
                DestroyMenu(menu);
            }

            if (menuObject is not null)
            {
                Marshal.ReleaseComObject(menuObject);
            }

            if (folderObject is not null)
            {
                Marshal.ReleaseComObject(folderObject);
            }

            foreach (var pidl in pidls)
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }
    }

    public static (int X, int Y) GetCursorPosition() =>
        GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);

    public static (int X, int Y) ClientToScreen(nint window, int x, int y)
    {
        var point = new Point { X = x, Y = y };
        ClientToScreen(window, ref point);
        return (point.X, point.Y);
    }

    private static IntPtr ParseDisplayName(string path)
    {
        Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _));
        return pidl;
    }

    private static string? GetVerb(IContextMenu menu, IntPtr offset)
    {
        var buffer = Marshal.AllocCoTaskMem(512 * sizeof(char));
        try
        {
            return menu.GetCommandString(offset, CommandStringFlags.VerbUnicode, IntPtr.Zero, buffer, 512) >= 0
                ? Marshal.PtrToStringUni(buffer)
                : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    private static InvokeMask ModifierMask()
    {
        var mask = (InvokeMask)0;
        if (GetKeyState(0x10) < 0)
        {
            mask |= InvokeMask.ShiftDown;
        }

        if (GetKeyState(0x11) < 0)
        {
            mask |= InvokeMask.ControlDown;
        }

        return mask;
    }

    private static IntPtr ForwardMenuMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data)
    {
        // Submenus such as Send To and Open With fill themselves in, and draw their icons, here.
        if (message is WindowMessage.InitMenuPopup or WindowMessage.DrawItem or WindowMessage.MeasureItem or WindowMessage.MenuChar)
        {
            if (s_activeMenu3 is not null && s_activeMenu3.HandleMenuMsg2(message, wParam, lParam, out var result) >= 0)
            {
                return result;
            }

            if (s_activeMenu2 is not null && s_activeMenu2.HandleMenuMsg(message, wParam, lParam) >= 0)
            {
                return IntPtr.Zero;
            }
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    private static class WindowMessage
    {
        public const uint DrawItem = 0x002B;
        public const uint MeasureItem = 0x002C;
        public const uint InitMenuPopup = 0x0117;
        public const uint MenuChar = 0x0120;
    }

    [Flags]
    private enum ContextMenuFlags : uint
    {
        Normal = 0x0,
        Explore = 0x4,
        ExtendedVerbs = 0x100
    }

    [Flags]
    private enum TrackPopupFlags : uint
    {
        RightButton = 0x0002,
        ReturnCommand = 0x0100
    }

    [Flags]
    private enum InvokeMask : uint
    {
        Unicode = 0x00004000,
        ShiftDown = 0x10000000,
        PointInvoke = 0x20000000,
        ControlDown = 0x40000000
    }

    private enum CommandStringFlags : uint
    {
        VerbUnicode = 0x4
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CommandInvokeInfo
    {
        public int cbSize;
        public InvokeMask fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectoryW;
        public IntPtr lpTitleW;
        public Point ptInvoke;
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr bindContext, [MarshalAs(UnmanagedType.LPWStr)] string displayName, out uint eaten, out IntPtr pidl, ref uint attributes);
        void EnumObjects(IntPtr hwnd, uint flags, out IntPtr enumIdList);
        void BindToObject(IntPtr pidl, IntPtr bindContext, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object result);
        void BindToStorage(IntPtr pidl, IntPtr bindContext, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object result);
        [PreserveSig] int CompareIDs(IntPtr parameter, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object result);
        void GetAttributesOf(uint count, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] pidls, ref uint attributes);
        void GetUIObjectOf(IntPtr hwndOwner, uint count, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] pidls, ref Guid riid, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out object result);
        void GetDisplayNameOf(IntPtr pidl, uint flags, IntPtr name);
        void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out IntPtr pidlOut);
    }

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint firstCommand, uint lastCommand, ContextMenuFlags flags);
        [PreserveSig] int InvokeCommand(ref CommandInvokeInfo info);
        [PreserveSig] int GetCommandString(IntPtr command, CommandStringFlags type, IntPtr reserved, IntPtr name, uint length);
    }

    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint firstCommand, uint lastCommand, ContextMenuFlags flags);
        [PreserveSig] int InvokeCommand(ref CommandInvokeInfo info);
        [PreserveSig] int GetCommandString(IntPtr command, CommandStringFlags type, IntPtr reserved, IntPtr name, uint length);
        [PreserveSig] int HandleMenuMsg(uint message, IntPtr wParam, IntPtr lParam);
    }

    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint firstCommand, uint lastCommand, ContextMenuFlags flags);
        [PreserveSig] int InvokeCommand(ref CommandInvokeInfo info);
        [PreserveSig] int GetCommandString(IntPtr command, CommandStringFlags type, IntPtr reserved, IntPtr name, uint length);
        [PreserveSig] int HandleMenuMsg(uint message, IntPtr wParam, IntPtr lParam);
        [PreserveSig] int HandleMenuMsg2(uint message, IntPtr wParam, IntPtr lParam, out IntPtr result);
    }

    private delegate IntPtr SubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr pidl, uint attributesIn, out uint attributesOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object result, out IntPtr lastPidl);

    [DllImport("shell32.dll")]
    private static extern int SHBindToObject(IntPtr shellFolder, IntPtr pidl, IntPtr bindContext, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object result);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr menu, TrackPopupFlags flags, int x, int y, IntPtr window, IntPtr parameters);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id, IntPtr data);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
