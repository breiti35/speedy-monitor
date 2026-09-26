using Microsoft.Win32;

namespace NetSpeedMonitor;

/// <summary>Verwaltet den Autostart-Eintrag im "Run"-Registry-Key des aktuellen Benutzers.</summary>
public static class AutostartHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NetSpeedMonitor";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value
                   && value.Trim('"').Equals(GetExePath(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLog.Error("Autostart-Status konnte nicht gelesen werden", ex);
            return false;
        }
    }

    /// <summary>Wirft nie - Fehler werden nur protokolliert.</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
                key.SetValue(ValueName, $"\"{GetExePath()}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Autostart konnte nicht {(enabled ? "aktiviert" : "deaktiviert")} werden", ex);
        }
    }

    private static string GetExePath() => Environment.ProcessPath ?? Application.ExecutablePath;
}
