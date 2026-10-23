using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Velopack;
using WinThunar.Services;

namespace WinThunar;

/// <summary>
/// Custom entry point so a second launch hands its folder request to the running instance
/// instead of paying WinUI startup again.
/// </summary>
public static class Program
{
#if ROADMAP_SMOKE_TEST
    private const string InstanceKey = "WinThunar.DesktopSmoke";
#else
    private const string InstanceKey = "WinThunar.Main";
#endif

    [STAThread]
    private static int Main(string[] args)
    {
        // Must run first: the installer launches the app with hook arguments and expects it to exit.
        // An all-users install needs administrator permission to update, so a pending update must not
        // raise a UAC prompt every time WinThunar starts; the update bar offers it instead.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(UpdateService.IsInstallFolderWritable())
            .OnBeforeUninstallFastCallback(_ => ReleaseShellRegistration())
            .Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (args is [var switchArgument] && TryRunRegistrationSwitch(switchArgument, out var exitCode))
        {
            return exitCode;
        }

#if !ROADMAP_SMOKE_TEST
        if (MsiMigrationStateGuard.GetStartupBlockReason() is { } migrationMessage)
        {
            _ = MigrationMessageBox(IntPtr.Zero, migrationMessage, "WinThunar installation", 0x10);
            return 1;
        }
#endif

        NotificationService.Initialize();
        if (TryRedirectToRunningInstance())
        {
            NotificationService.Shutdown();
            return 0;
        }

#if !ROADMAP_SMOKE_TEST
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

#endif
        var request = LaunchRequest.Parse(args);
        try
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == ExtendedActivationKind.AppNotification &&
                activation.Data is Microsoft.Windows.AppNotifications.AppNotificationActivatedEventArgs notification)
                request = NotificationService.RequestFromArguments(notification.Arguments);
        }
        catch (COMException)
        {
            // Shell activation services must not prevent a normal command-line launch.
        }
        Microsoft.UI.Xaml.Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(request);
        });
        NotificationService.Shutdown();
        return 0;
    }

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MigrationMessageBox(IntPtr owner, string message, string caption, uint type);

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

    /// <summary>
    /// Uninstalling deletes the executable, so Explorer must stop launching it. A portable copy's
    /// registration is left in place.
    /// </summary>
    private static void ReleaseShellRegistration()
    {
        if (!ShellRegistrationService.IsSupported || Environment.ProcessPath is not { } executable)
        {
            return;
        }

        try
        {
            new ShellRegistrationService(executable).UnregisterIfOwned();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Never block an uninstall; the stale verb is harmless and the next copy repairs it.
        }
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
