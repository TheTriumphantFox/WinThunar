using System.Runtime.InteropServices;
using System.Text;

namespace WinThunar.Services;

/// <summary>Desktop jump-list tasks work in both the portable and installed (unpackaged) app.</summary>
public static class TaskbarService
{
    public static string? LastError { get; private set; }
    public static void Initialize()
    {
        object? destination = null;
        object? collection = null;
        object? removed = null;
        var links = new List<object>();
        var begun = false;
        try
        {
            destination = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6"))!);
            var list = (ICustomDestinationList)destination!;
            var arrayId = typeof(IObjectArray).GUID;
            list.BeginList(out _, ref arrayId, out removed);
            begun = true;
            collection = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A"))!);
            var tasks = (IObjectCollection)collection!;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var (title, path) in new[] { ("New window", ""), ("Home", home), ("Downloads", Path.Combine(home, "Downloads")) })
            {
                if (path.Length > 0 && !Directory.Exists(path)) continue;
                var linkObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
                links.Add(linkObject);
                var link = (IShellLink)linkObject;
                link.SetPath(Environment.ProcessPath!);
                link.SetArguments(path.Length == 0 ? "" : "\"" + path.TrimEnd('\\') + "\"");
                link.SetDescription(title);
                link.SetIconLocation(Environment.ProcessPath!, 0);
                var key = new PropertyKey { Format = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), Id = 2 };
                var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(title) };
                try { ((IPropertyStore)linkObject).SetValue(ref key, ref value); ((IPropertyStore)linkObject).Commit(); }
                finally { PropVariantClear(ref value); }
                tasks.AddObject(linkObject);
            }
            list.AddUserTasks((IObjectArray)collection!);
            list.CommitList();
            begun = false;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
            if (begun) { try { ((ICustomDestinationList)destination!).AbortList(); } catch (COMException) { } }
        }
        finally
        {
            foreach (var link in links) Marshal.ReleaseComObject(link);
            if (removed is not null) Marshal.ReleaseComObject(removed);
            if (collection is not null) Marshal.ReleaseComObject(collection);
            if (destination is not null) Marshal.ReleaseComObject(destination);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant
    { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);

    [ComImport, Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
        void BeginList(out uint slots, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object removed);
        void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string name, IObjectArray items);
        void AppendKnownCategory(int category);
        void AddUserTasks(IObjectArray items);
        void CommitList();
        void GetRemovedDestinations(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object removed);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string appId);
        void AbortList();
    }
    [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectArray { void GetCount(out uint count); void GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value); }
    [ComImport, Guid("5632B1A4-E38A-400A-928A-D4CD63230295"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectCollection
    {
        void GetCount(out uint count);
        void GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value);
        void AddObject([MarshalAs(UnmanagedType.IUnknown)] object value);
        void AddFromArray(IObjectArray source);
        void RemoveObjectAt(uint index);
        void Clear();
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr data, uint flags);
        void GetIDList(out IntPtr list); void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetHotkey(out short key); void SetHotkey(short key);
        void GetShowCmd(out int command); void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
