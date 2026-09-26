using System.Diagnostics;
using System.Net.NetworkInformation;

namespace NetSpeedMonitor;

public readonly record struct NetworkSample(
    double UploadBytesPerSecond,
    double DownloadBytesPerSecond,
    long UploadBytesDelta,
    long DownloadBytesDelta);

/// <summary>
/// Pollt periodisch die Zaehler aller (oder eines gewaehlten) Netzwerkadapters
/// und berechnet daraus die aktuelle Transferrate.
/// </summary>
public sealed class NetworkMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Dictionary<string, (long Sent, long Received)> _lastCounters = new();
    // Monotone Zeitbasis: Uhrumstellungen/NTP-Korrekturen duerfen die Rate nicht verfaelschen.
    private long _lastSampleTimestamp;

    public event Action<NetworkSample>? SampleReady;

    public string AdapterId { get; set; } = "Auto";

    /// <summary>Anzeigenamen der Adapter, die im letzten Tick gezaehlt wurden.</summary>
    public IReadOnlyList<string> MonitoredAdapterNames { get; private set; } = Array.Empty<string>();

    public int IntervalMs
    {
        get => _timer.Interval;
        set => _timer.Interval = Math.Clamp(value, 100, 60_000);
    }

    public NetworkMonitor()
    {
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        ResetBaseline();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    /// <summary>
    /// Nach einem Adapterwechsel die Basiswerte neu einlesen, damit der naechste Tick
    /// keinen kuenstlichen Ausschlag durch unbekannte alte Zaehlerstaende erzeugt.
    /// </summary>
    public void ResetBaseline()
    {
        _lastCounters.Clear();
        foreach (var ni in GetCandidateInterfaces())
        {
            if (TryGetCounters(ni, out var counters))
                _lastCounters[ni.Id] = counters;
        }
        _lastSampleTimestamp = Stopwatch.GetTimestamp();
    }

    private IEnumerable<NetworkInterface> GetCandidateInterfaces()
    {
        var all = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                         && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

        return AdapterId == "Auto" ? all : all.Where(ni => ni.Id == AdapterId);
    }

    // IPv4 + IPv6. Ein Adapter kann zwischen Aufzaehlung und Abfrage verschwinden -> dann ueberspringen.
    private static bool TryGetCounters(NetworkInterface ni, out (long Sent, long Received) counters)
    {
        try
        {
            var stats = ni.GetIPStatistics();
            counters = (stats.BytesSent, stats.BytesReceived);
            return true;
        }
        catch (NetworkInformationException)
        {
            counters = default;
            return false;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedSeconds = Stopwatch.GetElapsedTime(_lastSampleTimestamp, now).TotalSeconds;
        if (elapsedSeconds <= 0)
            elapsedSeconds = IntervalMs / 1000.0;

        long uploadDelta = 0;
        long downloadDelta = 0;
        var seenIds = new HashSet<string>();
        var names = new List<string>();

        foreach (var ni in GetCandidateInterfaces())
        {
            if (!TryGetCounters(ni, out var counters))
                continue;

            seenIds.Add(ni.Id);
            names.Add(ni.Name);

            if (_lastCounters.TryGetValue(ni.Id, out var last))
            {
                var sentDelta = counters.Sent - last.Sent;
                var receivedDelta = counters.Received - last.Received;

                // Negative Deltas (z.B. nach Adapter-Reset oder Zaehlerueberlauf) werden ignoriert.
                if (sentDelta >= 0) uploadDelta += sentDelta;
                if (receivedDelta >= 0) downloadDelta += receivedDelta;
            }

            _lastCounters[ni.Id] = counters;
        }

        // Adapter, die nicht mehr aktiv/vorhanden sind, aus dem Cache entfernen.
        foreach (var staleId in _lastCounters.Keys.Except(seenIds).ToList())
            _lastCounters.Remove(staleId);

        _lastSampleTimestamp = now;
        MonitoredAdapterNames = names;

        var sample = new NetworkSample(
            UploadBytesPerSecond: uploadDelta / elapsedSeconds,
            DownloadBytesPerSecond: downloadDelta / elapsedSeconds,
            UploadBytesDelta: uploadDelta,
            DownloadBytesDelta: downloadDelta);

        SampleReady?.Invoke(sample);
    }

    public void Dispose() => _timer.Dispose();
}
