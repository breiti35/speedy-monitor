using System.IO;

namespace NetSpeedMonitor;

public static class AppPaths
{
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public static readonly string DataFolder = Path.Combine(AppData, "SpeedyMonitor");

    /// <summary>
    /// Uebernimmt Einstellungen/Statistik aus dem Ordner des frueheren Namens "NetSpeed Monitor",
    /// damit die Umbenennung keine Daten verliert. Muss vor dem ersten Laden aufgerufen werden.
    /// </summary>
    public static void MigrateLegacyDataFolder()
    {
        var legacy = Path.Combine(AppData, "NetSpeedMonitor");
        try
        {
            if (Directory.Exists(legacy) && !Directory.Exists(DataFolder))
                Directory.Move(legacy, DataFolder);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Alter Datenordner konnte nicht uebernommen werden: {legacy}", ex);
        }
    }
}
