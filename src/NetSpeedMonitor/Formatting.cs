namespace NetSpeedMonitor;

/// <summary>Formatiert Transferraten (Bytes/Sekunde) in der gewaehlten Einheit.</summary>
public static class SpeedFormatter
{
    public static string FormatFull(double bytesPerSecond, SpeedUnit unit)
    {
        var (value, unitText) = FormatParts(bytesPerSecond, unit);
        return $"{value} {unitText}";
    }

    /// <summary>Zahl und Einheit getrennt, damit sie in eigenen Spalten ausgerichtet werden koennen.</summary>
    public static (string Value, string Unit) FormatParts(double bytesPerSecond, SpeedUnit unit)
    {
        if (bytesPerSecond < 0) bytesPerSecond = 0;

        return unit switch
        {
            SpeedUnit.KBs => ($"{bytesPerSecond / 1024.0:0.0}", "KB/s"),
            SpeedUnit.MBs => ($"{bytesPerSecond / 1024.0 / 1024.0:0.00}", "MB/s"),
            SpeedUnit.Kbits => ($"{bytesPerSecond * 8.0 / 1000.0:0.0}", "kbit/s"),
            SpeedUnit.Mbits => ($"{bytesPerSecond * 8.0 / 1_000_000.0:0.00}", "Mbit/s"),
            _ => FormatAutoParts(bytesPerSecond)
        };
    }

    private static (string, string) FormatAutoParts(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return ($"{bytesPerSecond:0}", "B/s");

        var kb = bytesPerSecond / 1024.0;
        if (kb < 1024)
            return ($"{kb:0.0}", "KB/s");

        return ($"{kb / 1024.0:0.00}", "MB/s");
    }
}

/// <summary>Formatiert kumulierte Byte-Mengen (Tages-/Wochen-Traffic) menschenlesbar.</summary>
public static class ByteFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string FormatBytes(long bytes)
    {
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:0.##} {Units[unitIndex]}";
    }
}
