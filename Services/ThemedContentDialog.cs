using Microsoft.UI.Xaml.Controls;

namespace WinThunar.Services;

/// <summary>ContentDialog popups need an explicit override in addition to the window root's theme.</summary>
public sealed class ThemedContentDialog : ContentDialog
{
    public ThemedContentDialog()
    {
        DefaultStyleKey = typeof(ContentDialog);
        ApplyTheme(null, EventArgs.Empty);
        Opened += OnOpened;
        Closed += OnClosed;
        Unloaded += (_, _) => App.ThemeChanged -= ApplyTheme;
    }

    private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        ApplyTheme(null, EventArgs.Empty);
        App.ThemeChanged += ApplyTheme;
    }

    private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args) => App.ThemeChanged -= ApplyTheme;

    private void ApplyTheme(object? sender, EventArgs args) => RequestedTheme = App.GetElementTheme(App.ThemePreference);
}
