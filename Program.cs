using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using WinThunar.Services;

namespace WinThunar;

/// <summary>
/// Custom entry point so a second launch hands its folder request to the running instance
/// instead of paying WinUI startup again.
/// </summary>
public static class Program
{
    private const string InstanceKey = "WinThunar.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (TryRedirectToRunningInstance())
        {
            return 0;
        }

        var request = LaunchRequest.Parse(args);
        Microsoft.UI.Xaml.Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(request);
        });
        return 0;
    }

    private static bool TryRedirectToRunningInstance()
    {
        try
        {
            var keyInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
            if (keyInstance.IsCurrent)
            {
                keyInstance.Activated += App.OnRedirectedActivation;
                return false;
            }

            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            // Let the running instance bring its new window to the foreground.
            _ = AllowSetForegroundWindow(keyInstance.ProcessId);
            var redirected = false;
            var done = CreateEvent(IntPtr.Zero, true, false, null);
            try
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        keyInstance.RedirectActivationToAsync(activation).AsTask().Wait();
                        redirected = true;
                    }
                    catch (AggregateException)
                    {
                        // The running instance went away; fall back to a standalone window.
                    }
                    finally
                    {
                        SetEvent(done);
                    }
                });
                // Pump COM while waiting; a plain Wait on the STA thread can deadlock the redirect.
                _ = CoWaitForMultipleObjects(0, 0xFFFFFFFF, 1, [done], out _);
            }
            finally
            {
                CloseHandle(done);
            }

            return redirected;
        }
        catch (COMException)
        {
            // Redirection can fail across integrity levels (an elevated instance); run standalone.
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(IntPtr attributes, bool manualReset, bool initialState, string? name);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(
        uint flags,
        uint timeout,
        ulong handleCount,
        IntPtr[] handles,
        out uint signaledIndex);
}
