using System.IO;
using System.Text.Json;

namespace NetSpeedMonitor;

public enum SpeedUnit
{
    Auto,
    KBs,
    MBs,
    Kbits,
    Mbits
}

public sealed class AppSettings
{
    public string AdapterId { get; set; } = "Auto";
    public SpeedUnit Unit { get; set; } = SpeedUnit.Auto;
    public int UpdateIntervalMs { get; set; } = 1000;
    public bool AutostartEnabled { get; set; } = false;
    public bool ShowTaskbarOverlay { get; set; } = true;

    public AppSettings Clone() => new()
    {
        AdapterId = AdapterId,
        Unit = Unit,
        UpdateIntervalMs = UpdateIntervalMs,
        AutostartEnabled = AutostartEnabled,
        ShowTaskbarOverlay = ShowTaskbarOverlay
    };
}

public static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetSpeedMonitor", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // Bei defekter/ungueltiger Settings-Datei einfach auf Standardwerte zurueckfallen.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
