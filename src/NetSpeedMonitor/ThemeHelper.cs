using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NetSpeedMonitor;

/// <summary>Liest den Windows-Hell/Dunkel-Modus und passt Fenster-Titelleisten daran an.</summary>
public static class ThemeHelper
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Hell/Dunkel der Taskleiste und Shell-Oberflaechen.</summary>
    public static bool IsSystemLightTheme() => ReadFlag("SystemUsesLightTheme", defaultValue: false);

    /// <summary>Hell/Dunkel fuer normale App-Fenster (Einstellungen, Statistik).</summary>
    public static bool IsAppsLightTheme() => ReadFlag("AppsUseLightTheme", defaultValue: true);

    public static void ApplyTitleBarTheme(Form form, bool dark)
    {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        var value = dark ? 1 : 0;
        try
        {
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch
        {
            // Aeltere Windows-Builds ohne dieses Attribut - Titelleiste bleibt einfach hell.
        }
    }

    private static bool ReadFlag(string name, bool defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int v ? v != 0 : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
