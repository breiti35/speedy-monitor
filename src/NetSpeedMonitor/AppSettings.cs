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
    private static readonly string FilePath = Path.Combine(AppPaths.DataFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return Validate(settings);
            }
        }
        catch (Exception ex)
        {
            // Bei defekter/ungueltiger Settings-Datei auf Standardwerte zurueckfallen.
            AppLog.Error($"Einstellungen konnten nicht gelesen werden: {FilePath}", ex);
        }

        return new AppSettings();
    }

    private static AppSettings Validate(AppSettings settings)
    {
        settings.UpdateIntervalMs = Math.Clamp(settings.UpdateIntervalMs, 250, 5000);
        if (!Enum.IsDefined(settings.Unit))
            settings.Unit = SpeedUnit.Auto;
        if (string.IsNullOrWhiteSpace(settings.AdapterId))
            settings.AdapterId = "Auto";
        return settings;
    }

    /// <summary>Wirft nie - ein fehlgeschlagener Save wird nur protokolliert.</summary>
    public static void Save(AppSettings settings)
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("Einstellungen konnten nicht serialisiert werden", ex);
            return;
        }

        AppLog.TryWriteAtomic(FilePath, json, $"Einstellungen konnten nicht gespeichert werden: {FilePath}");
    }
}
