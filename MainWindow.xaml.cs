using Microsoft.UI.Xaml;

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
    public MainWindow(string? initialPath = null)
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version;
        Title = version is null
            ? "WinThunar"
            : $"WinThunar {version.Major}.{version.Minor}.{version.Build}";
        AppWindow.SetIcon("Assets/AppIcon.ico");

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage), initialPath);
    }
}
