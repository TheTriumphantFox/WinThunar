using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinThunar.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinThunar;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _closePromptOpen;

    public MainPage? Page => RootFrame.Content as MainPage;

    public MainWindow(string? initialPath = null)
        : this(initialPath is null ? LaunchRequest.Empty : new LaunchRequest(initialPath, null))
    {
    }

    public MainWindow(LaunchRequest launchRequest)
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version;
        Title = version is null
            ? "WinThunar"
            : version.Build > 0
                ? $"WinThunar {version.Major}.{version.Minor}.{version.Build}"
                : $"WinThunar {version.Major}.{version.Minor}";
        // Absolute: shortcuts and Explorer launch WinThunar with a working directory other than its own.
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));
        AppWindow.Closing += AppWindow_Closing;

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage), launchRequest);
    }

    private async void AppWindow_Closing(
        Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (RootFrame.Content is MainPage { HasPendingHistory: true })
        {
            args.Cancel = true;
            return;
        }
        if (_allowClose || RootFrame.Content is not MainPage { HasPendingTransfers: true } page)
        {
            return;
        }

        args.Cancel = true;
        if (_closePromptOpen)
        {
            return;
        }

        _closePromptOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = "File work is still running",
                Content = "Closing WinThunar will cancel active and queued transfers or interrupt recovery. Incomplete file work will be checked again the next time WinThunar starts.",
                PrimaryButtonText = "Close and stop file work",
                CloseButtonText = "Keep working",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = RootFrame.XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                page.CancelTransfersForClose();
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closePromptOpen = false;
        }
    }
}
