using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace WinThunar.Services;

/// <summary>ContentDialog popups need an explicit override in addition to the window root's theme.</summary>
public sealed class ThemedContentDialog : ContentDialog
{
    private readonly TypedEventHandler<object, WindowEventArgs> _hostClosed;
    private Window? _host;

    public ThemedContentDialog()
    {
        DefaultStyleKey = typeof(ContentDialog);
        _hostClosed = (_, _) => StopFollowingTheme();
        ApplyTheme(null, EventArgs.Empty);
        Opened += OnOpened;
        Closed += (_, _) => StopFollowingTheme();
    }

    private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        ApplyTheme(null, EventArgs.Empty);
        // WinUI raises Unloaded on a dialog that is still open, so Unloaded cannot end the subscription.
        // A window that closes under an open dialog never raises the dialog's Closed; its own Closed does.
        StopFollowingTheme();
        _host = App.FindWindow(XamlRoot);
        if (_host is null) return;
        _host.Closed += _hostClosed;
        App.ThemeChanged += ApplyTheme;
    }

    private void StopFollowingTheme()
    {
        App.ThemeChanged -= ApplyTheme;
        if (_host is not null) _host.Closed -= _hostClosed;
        _host = null;
    }

    private void ApplyTheme(object? sender, EventArgs args) => RequestedTheme = App.GetElementTheme(App.ThemePreference);
}
