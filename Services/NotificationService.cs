using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Runtime.InteropServices;

namespace WinThunar.Services;

/// <summary>Local transfer notifications; folder navigation is the only activation action.</summary>
public static class NotificationService
{
    private static bool _registered;
    private static bool _handlerAttached;
    public static bool IsRegistered => _registered;
    public static string? LastError { get; private set; }
    public static void Initialize()
    {
        if (_registered) return;
        LastError = null;
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnInvoked;
            _handlerAttached = true;
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            LastError = $"Windows notifications could not register (0x{ex.HResult:X8}): {ex.Message}";
            DetachHandler();
        }
    }

    private static void OnInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // Startup activations are delivered by AppInstance. This event handles a running app.
        if (App.DispatcherQueue is { } dispatcher)
            dispatcher.TryEnqueue(() => App.OpenWindow(RequestFromArguments(args.Arguments)));
    }

    public static LaunchRequest RequestFromArguments(IDictionary<string, string> arguments)
    {
        if (arguments.TryGetValue("folder", out var folder) && Path.IsPathFullyQualified(folder) && Directory.Exists(folder))
            return new LaunchRequest(folder, null);
        return LaunchRequest.Empty;
    }

    public static void TransferFinished(string title, string summary, string destination)
    {
        if (!_registered) return;
        try
        {
            var notification = new AppNotificationBuilder().AddText(title).AddText(summary)
                .AddArgument("folder", destination).BuildNotification();
            notification.Expiration = DateTimeOffset.Now.AddHours(1);
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        { LastError = ex.Message; }
    }

    public static void Shutdown()
    {
        try
        {
            if (_registered) AppNotificationManager.Default.Unregister();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        { LastError = $"Windows notifications could not unregister (0x{ex.HResult:X8}): {ex.Message}"; }
        finally
        {
            _registered = false;
            DetachHandler();
        }
    }

    private static void DetachHandler()
    {
        if (!_handlerAttached) return;
        AppNotificationManager.Default.NotificationInvoked -= OnInvoked;
        _handlerAttached = false;
    }
}
