using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace WinThunar.Services;

/// <summary>
/// The System colors theme lets the Mica backdrop through WinThunar's surfaces and tints selections with the
/// Windows accent color. It recolors shared brushes in place, so every open element follows without
/// re-resolving resources. High Contrast keeps its system colors.
/// </summary>
internal static class SystemColorsTheme
{
    private const string BackdropKey = "WinThunarBackdropBrush";
    private const string LayerKey = "WinThunarLayerBrush";
    private const string SelectedTabKey = "TabViewItemHeaderBackgroundSelected";
    // WinUI keys its dark dictionary "Default".
    private static readonly string[] Themes = ["Light", "Default"];
    // Windows 11's LayerFillColorDefault: a translucent card over Mica.
    private static readonly Color LightLayer = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);
    private static readonly Color DarkLayer = Color.FromArgb(0x4C, 0x3A, 0x3A, 0x3A);
    // WinUI's own values, restored when another theme is chosen: SubtleFillColorSecondary/Tertiary and
    // SolidBackgroundFillColorTertiary (selected tab). Re-check them on every Windows App SDK upgrade;
    // CONTRIBUTING.md ("Windows App SDK upgrades and the System colors theme") has the steps.
    private static readonly Color LightSecondary = Color.FromArgb(0x09, 0, 0, 0), LightTertiary = Color.FromArgb(0x06, 0, 0, 0);
    private static readonly Color DarkSecondary = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF), DarkTertiary = Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF);
    private static readonly Color LightTab = Color.FromArgb(0xFF, 0xF9, 0xF9, 0xF9), DarkTab = Color.FromArgb(0xFF, 0x28, 0x28, 0x28);
    // Lightweight-styling keys read by the WinUI list, grid, and tree item templates: the default fill
    // (true for SubtleFillColorSecondary) and the accent tint strength.
    private static readonly (string Key, bool Secondary, double Opacity)[] SelectionKeys =
    [
        ("ListViewItemBackgroundSelected", true, 0.2),
        ("ListViewItemBackgroundSelectedPointerOver", false, 0.26),
        ("ListViewItemBackgroundSelectedPressed", true, 0.16),
        ("GridViewItemBackgroundSelected", false, 0.2),
        ("GridViewItemBackgroundSelectedPointerOver", false, 0.26),
        ("GridViewItemBackgroundSelectedPressed", true, 0.16),
        ("TreeViewItemBackgroundSelected", true, 0.2),
        ("TreeViewItemBackgroundSelectedPointerOver", false, 0.26),
        ("TreeViewItemBackgroundSelectedPressed", true, 0.16)
    ];

    // Kept alive for ColorValuesChanged, which stops firing once the UISettings instance is collected.
    private static readonly UISettings Settings = new();
    // Control-template brushes, shared by every element passed to Attach.
    private static readonly Dictionary<(string Theme, string Key), SolidColorBrush> ControlBrushes = [];
    // App.xaml's surface brushes and their solid colors.
    private static readonly Dictionary<(string Theme, string Key), (SolidColorBrush Brush, Color Solid)> Surfaces = [];
    private static bool _initialized;

    internal static bool IsActive { get; private set; }

    internal static void Apply(bool active)
    {
        if (!_initialized) Initialize();
        if (active == IsActive) return;
        IsActive = active;
        Recolor(active);
    }

    /// <summary>
    /// Points an element's selection and selected-tab resources at the shared brushes. WinUI's templates read
    /// these from the control's own resources; overrides in the app's theme dictionaries do not reach them.
    /// </summary>
    internal static void Attach(FrameworkElement element)
    {
        if (!_initialized) Initialize();
        foreach (var theme in Themes)
        {
            var resources = new ResourceDictionary();
            foreach (var ((brushTheme, key), brush) in ControlBrushes)
            {
                if (brushTheme == theme) resources[key] = brush;
            }

            element.Resources.ThemeDictionaries[theme] = resources;
        }
    }

    /// <summary>The shared brush for a theme ("Light" or "Default") and key; the desktop smoke checks compare against it.</summary>
    internal static SolidColorBrush ControlBrush(string theme, string key)
    {
        if (!_initialized) Initialize();
        return ControlBrushes[(theme, key)];
    }

    private static void Initialize()
    {
        _initialized = true;
        foreach (var theme in Themes)
        {
            var resources = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries[theme];
            foreach (var key in new[] { BackdropKey, LayerKey })
            {
                var brush = (SolidColorBrush)resources[key];
                Surfaces[(theme, key)] = (brush, brush.Color);
            }

            ControlBrushes[(theme, SelectedTabKey)] = new SolidColorBrush();
            foreach (var (key, _, _) in SelectionKeys) ControlBrushes[(theme, key)] = new SolidColorBrush();
        }

        Recolor(false);

        // Raised off the UI thread when the accent or app mode changes in Windows Settings.
        Settings.ColorValuesChanged += (_, _) => App.DispatcherQueue.TryEnqueue(() =>
        {
            if (IsActive) UpdateAccent();
        });
    }

    private static void Recolor(bool active)
    {
        foreach (var ((theme, key), (brush, solid)) in Surfaces)
        {
            brush.Color = !active ? solid : key == BackdropKey ? Colors.Transparent : Layer(theme);
        }

        foreach (var theme in Themes)
        {
            var light = theme == "Light";
            ControlBrushes[(theme, SelectedTabKey)].Color = active ? Layer(theme) : light ? LightTab : DarkTab;
            foreach (var (key, secondary, opacity) in SelectionKeys)
            {
                var brush = ControlBrushes[(theme, key)];
                brush.Opacity = active ? opacity : 1;
                if (!active) brush.Color = secondary ? light ? LightSecondary : DarkSecondary : light ? LightTertiary : DarkTertiary;
            }
        }

        if (active) UpdateAccent();
    }

    private static Color Layer(string theme) => theme == "Light" ? LightLayer : DarkLayer;

    private static void UpdateAccent()
    {
        var light = Settings.GetColorValue(UIColorType.AccentDark1);
        var dark = Settings.GetColorValue(UIColorType.AccentLight2);
        foreach (var theme in Themes)
        {
            foreach (var (key, _, _) in SelectionKeys) ControlBrushes[(theme, key)].Color = theme == "Light" ? light : dark;
        }
    }
}
