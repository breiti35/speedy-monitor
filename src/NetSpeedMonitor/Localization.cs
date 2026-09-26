using System.Globalization;

namespace NetSpeedMonitor;

public enum AppLanguage
{
    Auto,
    German,
    English
}

/// <summary>
/// Alle sichtbaren UI-Texte (Deutsch/Englisch). Bewusst ohne resx: jeder Text ist eine
/// Eigenschaft und damit vom Compiler geprueft.
/// </summary>
public static class L
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    public static bool IsGerman { get; private set; } = true;

    /// <summary>Nur fuer Texte mit Woertern (Wochentage, Monate) - Zahlen bleiben bei CurrentCulture.</summary>
    public static CultureInfo Culture => IsGerman ? GermanCulture : EnglishCulture;

    public static void Apply(AppLanguage language) => IsGerman = language switch
    {
        AppLanguage.German => true,
        AppLanguage.English => false,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de"
    };

    private static string T(string de, string en) => IsGerman ? de : en;

    // Kontextmenue
    public static string MenuShowStats => T("Statistik anzeigen…", "Show statistics…");
    public static string MenuUnit => T("Einheit", "Unit");
    public static string MenuAdapter => T("Netzwerkadapter", "Network adapter");
    public static string MenuAutostart => T("Mit Windows starten", "Start with Windows");
    public static string MenuAllMonitors => T("Auf allen Monitoren anzeigen", "Show on all monitors");
    public static string MenuSettings => T("Einstellungen…", "Settings…");
    public static string MenuResetStats => T("Statistik zurücksetzen…", "Reset statistics…");
    public static string MenuAbout => T("Über Speedy Monitor", "About Speedy Monitor");
    public static string MenuExit => T("Beenden", "Exit");
    public static string Automatic => T("Automatisch", "Automatic");
    public static string AdapterAutoShort => T("Automatisch (alle aktiven)", "Automatic (all active)");
    public static string Inactive => T("inaktiv", "inactive");
    public static string Active => T("aktiv", "active");

    // Tooltip des Panels
    public static string TooltipToday => T("Heute:", "Today:");

    // Statistik zuruecksetzen
    public static string ResetHeading => T("Statistik zurücksetzen?", "Reset statistics?");
    public static string ResetMessage => T(
        "Alle gespeicherten Datenmengen (Sitzung, Tage, Woche und Monat) werden unwiderruflich gelöscht.",
        "All recorded data amounts (session, days, week and month) will be permanently deleted.");
    public static string ResetConfirm => T("Zurücksetzen", "Reset");
    public static string Cancel => T("Abbrechen", "Cancel");
    public static string Close => T("Schließen", "Close");

    // Statistik-Flyout
    public static string StatsTitle => T("Speedy Monitor – Statistik", "Speedy Monitor – Statistics");
    public static string RowSession => T("Sitzung", "Session");
    public static string RowToday => T("Heute", "Today");
    public static string RowWeek => T("Woche (7 Tage)", "Week (7 days)");
    public static string RowMonth => T("Monat", "Month");
    public static string HistoryLink => T("Verlauf…", "History…");
    public static string NoActiveAdapter => T("Kein aktiver Adapter", "No active adapter");
    public static string SessionSince(string duration) => T($" · seit {duration}", $" · up {duration}");

    // Verlauf
    public static string HistoryTitle => T("Speedy Monitor – Verlauf", "Speedy Monitor – History");
    public static string HistoryHeading => T("Datenverbrauch", "Data usage");
    public static string Range30Days => T("30 Tage", "30 days");
    public static string Range90Days => T("90 Tage", "90 days");
    public static string Range12Months => T("12 Monate", "12 months");
    public static string TotalUpload => T("Upload gesamt", "Total upload");
    public static string TotalDownload => T("Download gesamt", "Total download");
    public static string AvgUploadPerDay => T("Ø Upload pro Tag", "Ø Upload per day");
    public static string AvgDownloadPerDay => T("Ø Download pro Tag", "Ø Download per day");
    public static string ExportCsv => T("Als CSV exportieren…", "Export as CSV…");
    public static string ExportDialogTitle => T("Verlauf als CSV exportieren", "Export history as CSV");
    public static string ExportFilter => T("CSV-Datei (*.csv)|*.csv|Alle Dateien (*.*)|*.*", "CSV file (*.csv)|*.csv|All files (*.*)|*.*");
    public static string ExportFilePrefix => T("SpeedyMonitor_Verlauf_", "SpeedyMonitor_History_");
    public static string CsvHeader => T("Datum;Upload (Bytes);Download (Bytes);Upload;Download", "Date;Upload (bytes);Download (bytes);Upload;Download");
    public static string Exported(string fileName) => T($"Exportiert: {fileName}", $"Exported: {fileName}");
    public static string ExportFailed => T(
        "Export fehlgeschlagen – ist die Datei noch in einem anderen Programm geöffnet?",
        "Export failed – is the file still open in another program?");
    public static string PerDay => T("Pro Tag", "Per day");
    public static string PerMonth => T("Pro Monat", "Per month");
    public static string NoData => T("Keine Daten", "No data");
    public static string DaysRecorded(int days) => IsGerman
        ? (days == 1 ? "1 Tag erfasst" : $"{days} Tage erfasst")
        : (days == 1 ? "1 day recorded" : $"{days} days recorded");
    public static string AxisDayFormat => T("dd.MM.", "MM/dd");
    public static string AxisDaySample => T("28.08.", "08/28");
    public static string TooltipDayFormat => T("dddd, dd.MM.yyyy", "dddd, MM/dd/yyyy");

    // Einstellungen
    public static string SettingsTitle => T("Speedy Monitor – Einstellungen", "Speedy Monitor – Settings");
    public static string GroupMeasurement => T("Messung", "Measurement");
    public static string GroupDisplay => T("Anzeige", "Display");
    public static string GroupSystem => T("System", "System");
    public static string UpdateInterval => T("Aktualisierungsintervall", "Update interval");
    public static string UnitAuto => T("Automatisch (B/s, KB/s, MB/s)", "Automatic (B/s, KB/s, MB/s)");
    public static string AdapterAutoLong => T("Automatisch (alle aktiven Adapter)", "Automatic (all active adapters)");
    // Immer zweisprachig, damit man die Einstellung in jeder Sprache findet.
    public const string LanguageLabel = "Sprache / Language";
    public static string LanguageAuto => T("Automatisch (Windows-Sprache)", "Automatic (Windows language)");
    public const string LanguageGerman = "Deutsch";
    public const string LanguageEnglish = "English";
    public static string Defaults => T("Standard", "Defaults");

    // Info-Dialog
    public static string Version(string version) => $"Version {version}";
    public static string Credits => T(
        "Entwickler: breiti35\nCode-Mitarbeit: Claude (Anthropic)",
        "Developer: breiti35\nCode contribution: Claude (Anthropic)");
    public static string StoryHeading => T("Wie es dazu kam", "How it came about");
    public static string Story => T(
        "Unter Windows 10 hatte ich immer den NetSpeed Monitor unten in der Taskleiste – ein kurzer "
        + "Blick genügte, und ich wusste, was im Netzwerk los ist. Mit Windows 11 war damit Schluss: "
        + "Die Technik dahinter gibt es nicht mehr, und keine der Alternativen hat mich wirklich überzeugt. "
        + "Also habe ich mir gedacht: Dann baue ich mir eben selbst eins – genau so, wie ich es haben will.",
        "On Windows 10 I always had NetSpeed Monitor in my taskbar – one quick glance and I knew what was "
        + "going on in my network. With Windows 11 that was over: the technology behind it no longer exists, "
        + "and none of the alternatives really convinced me. So I thought: then I'll just build one myself – "
        + "exactly the way I want it.");
    public static string UsageHint => T(
        "Klick auf das Panel öffnet die Statistik, Doppelklick die Einstellungen, Rechtsklick das Menü.",
        "Click the panel for statistics, double-click for settings, right-click for the menu.");
    public static string DataFolderCaption => T("Datenordner (Einstellungen und Statistik):", "Data folder (settings and statistics):");
    public static string OpenFolder => T("Ordner öffnen", "Open folder");
}
