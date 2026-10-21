using System.Collections.Concurrent;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinThunar.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinThunar;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>The process-wide updater shared by every window.</summary>
    public static UpdateService Updates { get; private set; } = null!;

    /// <summary>True while any window is copying or moving files, which a restart would cancel.</summary>
    public static bool HasPendingTransfers =>
        OpenWindows.OfType<MainWindow>().Any(window => window.Page?.HasPendingTransfers == true);

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    private static readonly ConcurrentQueue<LaunchRequest> PendingRedirects = new();
    private static readonly List<Window> OpenWindows = [];
    private readonly LaunchRequest _launchRequest;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App(LaunchRequest launchRequest)
    {
        _launchRequest = launchRequest;
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinThunar");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "crash.log"),
                $"{DateTimeOffset.Now:O}{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Never hide the original application failure because crash logging failed.
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Updates = new UpdateService();
        TaskbarService.Initialize();
        Window = OpenWindow(_launchRequest);
        OpenPendingRedirects();
#if !ROADMAP_SMOKE_TEST
        Updates.StartAutomaticChecks();
#endif
    }

    /// <summary>
    /// Receives launches that a second WinThunar process redirected here. Raised off the UI thread,
    /// possibly before <see cref="OnLaunched"/> has run.
    /// </summary>
    public static void OnRedirectedActivation(object? sender, AppActivationArguments args)
    {
        var request = args.Kind == ExtendedActivationKind.Launch &&
            args.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch
                ? LaunchRequest.ParseCommandLine(launch.Arguments)
                : args.Kind == ExtendedActivationKind.AppNotification &&
                  args.Data is Microsoft.Windows.AppNotifications.AppNotificationActivatedEventArgs notification
                    ? NotificationService.RequestFromArguments(notification.Arguments)
                    : LaunchRequest.Empty;
        PendingRedirects.Enqueue(request);
        DispatcherQueue?.TryEnqueue(OpenPendingRedirects);
    }

    /// <summary>Opens a top-level window that lives until the user closes it.</summary>
    public static Window OpenWindow(LaunchRequest request)
    {
        var window = new MainWindow(request);
        OpenWindows.Add(window);
        window.Closed += (_, _) =>
        {
            OpenWindows.Remove(window);
            if (ReferenceEquals(Window, window) && OpenWindows.Count > 0)
            {
                Window = OpenWindows[^1];
            }
            else if (OpenWindows.Count == 0)
            {
                // The process exits with its last window; install a downloaded update after it does.
                Updates.ApplyOnExit();
            }
        };
        window.Activate();
        return window;
    }

    private static void OpenPendingRedirects()
    {
        while (PendingRedirects.TryDequeue(out var request))
        {
            Window = OpenWindow(request);
        }
    }
}
