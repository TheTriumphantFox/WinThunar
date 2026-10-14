using System.Runtime.InteropServices;

namespace WinThunar.Services;

/// <summary>
/// Runs file operations through the Windows copy engine (IFileOperation). Used as a fallback for
/// items WinThunar's own engine cannot handle: locations that need administrator permission, where
/// the engine shows the UAC prompt itself, and folder links, which it handles as Explorer does.
/// These operations are not added to WinThunar's undo history.
/// </summary>
public static class ShellFileOperation
{
    private const uint NoConfirmMakeDirectory = 0x0200;
    private const uint NoConfirmation = 0x0010;
    private const uint AllowUndo = 0x0040;
    private const uint RecycleOnDelete = 0x00080000;
    private const uint RequireElevation = 0x10000000;
    private const int UserCancelled = unchecked((int)0x80270000);
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    public enum Kind
    {
        Copy,
        Move,
        Recycle,
        Delete
    }

    /// <summary>
    /// Returns false when the user cancelled (including declining UAC) or Windows aborted part of it.
    /// </summary>
    public static bool Run(nint ownerWindow, Kind kind, IReadOnlyList<string> paths, string? destination, bool elevate)
    {
        return Perform(ownerWindow, elevate, kind switch
        {
            Kind.Recycle => AllowUndo | RecycleOnDelete | NoConfirmation,
            Kind.Delete => NoConfirmation,
            _ => NoConfirmMakeDirectory
        }, operation =>
        {
            var destinationItem = destination is null ? null : CreateItem(destination);
            try
            {
                foreach (var path in paths)
                {
                    var item = CreateItem(path);
                    try
                    {
                        switch (kind)
                        {
                            case Kind.Copy:
                                operation.CopyItem(item, destinationItem!, null, IntPtr.Zero);
                                break;
                            case Kind.Move:
                                operation.MoveItem(item, destinationItem!, null, IntPtr.Zero);
                                break;
                            default:
                                operation.DeleteItem(item, IntPtr.Zero);
                                break;
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(item);
                    }
                }
            }
            finally
            {
                if (destinationItem is not null)
                {
                    Marshal.ReleaseComObject(destinationItem);
                }
            }
        });
    }

    public static bool CreateFolder(nint ownerWindow, string parent, string name, bool elevate) =>
        Perform(ownerWindow, elevate, NoConfirmMakeDirectory, operation =>
        {
            var parentItem = CreateItem(parent);
            try
            {
                operation.NewItem(parentItem, (uint)FileAttributes.Directory, name, null, IntPtr.Zero);
            }
            finally
            {
                Marshal.ReleaseComObject(parentItem);
            }
        });

    public static bool Rename(nint ownerWindow, string path, string newName, bool elevate) =>
        Perform(ownerWindow, elevate, 0, operation =>
        {
            var item = CreateItem(path);
            try
            {
                operation.RenameItem(item, newName, IntPtr.Zero);
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        });

    private static bool Perform(nint ownerWindow, bool elevate, uint flags, Action<IFileOperation> queue)
    {
        var operation = (IFileOperation)Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("3ad05575-8857-4850-9277-11b85bdb8e09"))!)!;
        try
        {
            operation.SetOwnerWindow(ownerWindow);
            // WinThunar already knows access was denied, so ask for elevation up front rather than
            // showing Windows' "You'll need to provide administrator permission" step first.
            operation.SetOperationFlags(flags | (elevate ? RequireElevation : 0));
            queue(operation);
            var result = operation.PerformOperations();
            if (result is UserCancelled or ErrorCancelled)
            {
                return false;
            }

            Marshal.ThrowExceptionForHR(result);
            operation.GetAnyOperationsAborted(out var aborted);
            return !aborted;
        }
        finally
        {
            Marshal.ReleaseComObject(operation);
        }
    }

    private static IShellItem CreateItem(string path)
    {
        var guid = typeof(IShellItem).GUID;
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref guid, out var item));
        return (IShellItem)item;
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
    }

    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IntPtr sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr dialog);
        void SetProperties(IntPtr properties);
        void SetOwnerWindow(IntPtr owner);
        void ApplyPropertiesToItem(IShellItem item);
        void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] object items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr sink);
        void RenameItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        void MoveItem(IShellItem item, IShellItem destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? newName, IntPtr sink);
        void MoveItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destinationFolder);
        void CopyItem(IShellItem item, IShellItem destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? copyName, IntPtr sink);
        void CopyItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destinationFolder);
        void DeleteItem(IShellItem item, IntPtr sink);
        void DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object items);
        void NewItem(IShellItem destinationFolder, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? templateName, IntPtr sink);
        [PreserveSig] int PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object item);
}
