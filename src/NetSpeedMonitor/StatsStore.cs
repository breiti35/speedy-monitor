using System.IO;
using System.Linq;
using System.Text.Json;

namespace NetSpeedMonitor;

public sealed class DayTotal
{
    public DateTime Date { get; set; }
    public long UploadBytes { get; set; }
    public long DownloadBytes { get; set; }
}

public sealed class StatsData
{
    public DayTotal Today { get; set; } = new() { Date = DateTime.Now.Date };
    public List<DayTotal> History { get; set; } = new();
}

/// <summary>
/// Verwaltet Sitzungs-, Tages-, Wochen- und Monats-Traffic. Rollt bei Tageswechsel
/// automatisch den "Heute"-Zaehler in die Historie weiter.
/// </summary>
public sealed class StatsStore
{
    // Genug fuer Wochen- und kompletten Kalendermonats-Wert.
    private const int HistoryDays = 62;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetSpeedMonitor", "stats.json");

    private readonly StatsData _data;

    public StatsStore()
    {
        _data = Load();
        RolloverIfNeeded();
    }

    public DayTotal Today => _data.Today;

    public DateTime SessionStart { get; } = DateTime.Now;

    public (long Upload, long Download) SessionTotal => (_sessionUpload, _sessionDownload);

    private long _sessionUpload;
    private long _sessionDownload;

    /// <summary>Letzte 7 Tage inklusive heute.</summary>
    public (long Upload, long Download) WeekTotal() => SumSince(DateTime.Now.Date.AddDays(-6));

    /// <summary>Aktueller Kalendermonat.</summary>
    public (long Upload, long Download) MonthTotal()
    {
        var today = DateTime.Now.Date;
        return SumSince(new DateTime(today.Year, today.Month, 1));
    }

    private (long Upload, long Download) SumSince(DateTime from)
    {
        long up = _data.Today.UploadBytes;
        long down = _data.Today.DownloadBytes;
        foreach (var day in _data.History.Where(d => d.Date >= from))
        {
            up += day.UploadBytes;
            down += day.DownloadBytes;
        }
        return (up, down);
    }

    public void AddSample(long uploadBytes, long downloadBytes)
    {
        RolloverIfNeeded();
        _data.Today.UploadBytes += uploadBytes;
        _data.Today.DownloadBytes += downloadBytes;
        _sessionUpload += uploadBytes;
        _sessionDownload += downloadBytes;
    }

    /// <summary>Loescht alle gespeicherten Statistiken (inkl. Sitzung) und speichert sofort.</summary>
    public void Reset()
    {
        _data.History.Clear();
        _data.Today = new DayTotal { Date = DateTime.Now.Date };
        _sessionUpload = 0;
        _sessionDownload = 0;
        Save();
    }

    private void RolloverIfNeeded()
    {
        var today = DateTime.Now.Date;
        if (_data.Today.Date == today)
            return;

        if (_data.Today.Date != default)
            _data.History.Insert(0, _data.Today);

        var cutoff = today.AddDays(-HistoryDays);
        _data.History.RemoveAll(d => d.Date < cutoff);
        if (_data.History.Count > HistoryDays)
            _data.History.RemoveRange(HistoryDays, _data.History.Count - HistoryDays);

        _data.Today = new DayTotal { Date = today };
    }

    private static StatsData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<StatsData>(json);
                if (data is not null)
                {
                    // "null"-Eintraege in handeditierten/teilweise defekten Dateien wuerden spaeter NREs ausloesen.
                    data.Today ??= new DayTotal { Date = DateTime.Now.Date };
                    data.History = data.History?.Where(d => d is not null).ToList() ?? new();
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            // Bei defekter Statistikdatei neu beginnen.
            AppLog.Error($"Statistikdatei konnte nicht gelesen werden: {FilePath}", ex);
        }

        return new StatsData();
    }

    /// <summary>Wirft nie - Statistik ist nicht kritisch.</summary>
    public void Save()
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("Statistik konnte nicht serialisiert werden", ex);
            return;
        }

        AppLog.TryWriteAtomic(FilePath, json, $"Statistik konnte nicht gespeichert werden: {FilePath}");
    }
}
