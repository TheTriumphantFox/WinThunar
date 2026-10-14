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
        if (args is [var switchArgument] && TryRunRegistrationSwitch(switchArgument, out var exitCode))
        {
            return exitCode;
        }

        if (TryRedirectToRunningInstance())
        {
            return 0;
        }

        if (ShellRegistrationService.IsSupported && Environment.ProcessPath is { } executable)
        {
            try
            {
                new ShellRegistrationService(executable).RepairExecutablePath();
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                // Startup must not depend on registry access.
            }
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

    /// <summary>
    /// Scriptable registration, including the recovery path that works even when the UI cannot start.
    /// </summary>
    private static bool TryRunRegistrationSwitch(string argument, out int exitCode)
    {
        exitCode = 0;
        var action = argument.ToLowerInvariant() switch
        {
            ShellRegistrationService.UnregisterSwitch => (Action<ShellRegistrationService>)(service => service.SetOpenVerb(false)),
            ShellRegistrationService.RegisterSwitch => service => service.SetOpenVerb(true),
            ShellRegistrationService.RegisterDefaultSwitch => service => service.SetDefaultFileManager(true),
            _ => null
        };
        if (action is null)
        {
            return false;
        }

        if (!ShellRegistrationService.IsSupported || Environment.ProcessPath is not { } executable)
        {
            exitCode = 1;
            return true;
        }

        try
        {
            action(new ShellRegistrationService(executable));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            exitCode = 1;
        }

        return true;
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
