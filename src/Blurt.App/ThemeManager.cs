using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Blurt.App;

/// <summary>
/// Hand-rolled light/dark theming that follows the Windows system setting
/// (issue 19) — no theming NuGet packages. One shared <see cref="ResourceDictionary"/>
/// instance holds the semantic colour brushes plus the control templates from
/// Themes/Controls.xaml; every window merges that same instance and references
/// brushes via DynamicResource, so swapping the colour sub-dictionary on a system
/// theme change restyles all open windows in place. There is no
/// <see cref="System.Windows.Application"/> in this WinForms-hosted process, so the
/// dictionary is merged per-window rather than app-wide.
/// </summary>
internal static class ThemeManager
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE: makes the non-client title bar follow dark
    // mode. 20 on Windows 10 20H1+ / Windows 11.
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    private static readonly ResourceDictionary Shared = BuildShared();
    private static bool _dark;

    /// <summary>Raised after the colour palette swapped on a system theme change,
    /// so windows can refresh their non-client (title bar) appearance.</summary>
    public static event EventHandler? ThemeChanged;

    private static ResourceDictionary BuildShared()
    {
        _dark = SystemPrefersDark();
        var shared = new ResourceDictionary();
        shared.MergedDictionaries.Add(BuildPalette(_dark));
        shared.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Blurt;component/Themes/Controls.xaml", UriKind.Relative),
        });

        // Theme follows system, live: when the user flips Windows light/dark the
        // palette is swapped inside the shared instance and every DynamicResource
        // reference updates without reopening windows.
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category != UserPreferenceCategory.General)
                return;
            var dark = SystemPrefersDark();
            if (dark == _dark)
                return;
            _dark = dark;
            Shared.MergedDictionaries[0] = BuildPalette(dark);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        };

        return shared;
    }

    /// <summary>
    /// Merge the shared theme into <paramref name="window"/> and keep its title bar
    /// in sync with the palette. Call at the top of the window's constructor,
    /// before InitializeComponent, so the XAML's StaticResource references to the
    /// shared styles resolve during parse. Backgrounds stay in each window's XAML
    /// (the overlay pill must remain transparent).
    /// </summary>
    public static void Apply(Window window)
    {
        window.Resources.MergedDictionaries.Add(Shared);

        void ApplyTitleBar(object? s, EventArgs e) => SetTitleBar(window, _dark);
        window.SourceInitialized += ApplyTitleBar;
        ThemeChanged += ApplyTitleBar;
        window.Closed += (_, _) => ThemeChanged -= ApplyTitleBar;
    }

    private static void SetTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        var value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    // Windows stores the per-app light/dark choice here; 0 = dark, 1 (or missing,
    // pre-1903) = light.
    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;   // can't read the hive → default to light
        }
    }

    // The semantic palette. Keys are what the XAML references via DynamicResource;
    // both variants define the identical key set so a swap is seamless. Values
    // approximate the Windows 11 light/dark neutrals with the default blue accent.
    private static ResourceDictionary BuildPalette(bool dark)
    {
        var d = new ResourceDictionary();
        void Add(string key, string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            d[key] = brush;
        }

        if (dark)
        {
            Add("WindowBackgroundBrush", "#FF202020");
            Add("CardBackgroundBrush", "#FF2B2B2B");
            Add("CardBorderBrush", "#FF1D1D1D");
            Add("TextPrimaryBrush", "#FFFFFFFF");
            Add("TextSecondaryBrush", "#FFC8C8C8");
            Add("TextTertiaryBrush", "#FF9D9D9D");
            Add("ControlBackgroundBrush", "#FF373737");
            Add("ControlHoverBackgroundBrush", "#FF3F3F3F");
            Add("ControlPressedBackgroundBrush", "#FF2F2F2F");
            Add("ControlBorderBrush", "#FF4A4A4A");
            Add("ControlHoverBorderBrush", "#FF5A5A5A");
            Add("AccentBrush", "#FF4CC2FF");
            Add("AccentHoverBrush", "#FF47B5EE");
            Add("AccentPressedBrush", "#FF3FA1D4");
            Add("AccentTextBrush", "#FF003049");
            Add("PopupBackgroundBrush", "#FF2C2C2C");
            Add("ItemHoverBrush", "#FF383838");
            Add("ItemSelectedBrush", "#FF404040");
            Add("DangerTextBrush", "#FFFF99A4");
            Add("DangerBackgroundBrush", "#FF3B2326");
            Add("DangerBorderBrush", "#FF5E3A3F");
            Add("TrackBrush", "#FF454545");
            Add("ScrollThumbBrush", "#FF5F5F5F");
            Add("PillBackgroundBrush", "#E62B2B2B");
            Add("PillBorderBrush", "#FF454545");
            Add("PillTextBrush", "#FFFFFFFF");
        }
        else
        {
            Add("WindowBackgroundBrush", "#FFF6F6F6");
            Add("CardBackgroundBrush", "#FFFFFFFF");
            Add("CardBorderBrush", "#FFE7E7E7");
            Add("TextPrimaryBrush", "#FF1B1B1B");
            Add("TextSecondaryBrush", "#FF595959");
            Add("TextTertiaryBrush", "#FF8A8A8A");
            Add("ControlBackgroundBrush", "#FFFFFFFF");
            Add("ControlHoverBackgroundBrush", "#FFF6F6F6");
            Add("ControlPressedBackgroundBrush", "#FFEFEFEF");
            Add("ControlBorderBrush", "#FFD6D6D6");
            Add("ControlHoverBorderBrush", "#FFB8B8B8");
            Add("AccentBrush", "#FF0067C0");
            Add("AccentHoverBrush", "#FF1975C5");
            Add("AccentPressedBrush", "#FF12598F");
            Add("AccentTextBrush", "#FFFFFFFF");
            Add("PopupBackgroundBrush", "#FFFFFFFF");
            Add("ItemHoverBrush", "#FFF2F2F2");
            Add("ItemSelectedBrush", "#FFEAEAEA");
            Add("DangerTextBrush", "#FFB3261E");
            Add("DangerBackgroundBrush", "#FFFDECEC");
            Add("DangerBorderBrush", "#FFEFC3C3");
            Add("TrackBrush", "#FFE9E9E9");
            Add("ScrollThumbBrush", "#FFBFBFBF");
            Add("PillBackgroundBrush", "#F2FFFFFF");
            Add("PillBorderBrush", "#FFDDDDDD");
            Add("PillTextBrush", "#FF1B1B1B");
        }

        return d;
    }
}
