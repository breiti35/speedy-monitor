using Microsoft.Win32;

namespace NetSpeedMonitor;

/// <summary>Verwaltet den Autostart-Eintrag im "Run"-Registry-Key des aktuellen Benutzers.</summary>
public static class AutostartHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpeedyMonitor";
    private const string LegacyValueName = "NetSpeedMonitor";

    /// <summary>
    /// Registry ist die Quelle der Wahrheit (der Installer setzt den Eintrag ggf. selbst).
    /// Zeigt ein vorhandener Eintrag auf einen anderen Pfad (exe verschoben/neu installiert),
    /// wird er auf die aktuelle exe umgebogen. Gibt zurueck, ob Autostart aktiv ist.
    /// </summary>
    public static bool Synchronize()
    {
        bool hasEntry;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            hasEntry = key?.GetValue(ValueName) is string;
        }
        catch (Exception ex)
        {
            AppLog.Error("Autostart-Status konnte nicht gelesen werden", ex);
            return false;
        }

        if (hasEntry && !IsEnabled())
            SetEnabled(true);
        return hasEntry;
    }

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

            // Eintrag aus der Zeit vor der Umbenennung zeigt auf eine nicht mehr existierende exe.
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

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
