using System.IO;

namespace NetSpeedMonitor;

/// <summary>Minimales Fehlerprotokoll unter %AppData%\NetSpeedMonitor\error.log. Wirft nie.</summary>
public static class AppLog
{
    private const long MaxBytes = 256 * 1024;

    private static readonly object Sync = new();

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetSpeedMonitor", "error.log");

    public static void Error(string context, Exception? ex = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);

                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}";
                if (ex is not null)
                    entry += ex + Environment.NewLine;
                File.AppendAllText(FilePath, entry + Environment.NewLine);
            }
        }
        catch
        {
            // Logging darf nie selbst einen Fehler ausloesen.
        }
    }

    /// <summary>
    /// Schreibt Text atomar: erst in eine Temp-Datei im selben Ordner, dann per Move ersetzen,
    /// damit ein Absturz mitten im Schreiben die Zieldatei nicht zerstoert. Wirft nie.
    /// </summary>
    public static bool TryWriteAtomic(string path, string content, string context)
    {
        var tmp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(tmp, content);
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Error(context, ex);
            try { File.Delete(tmp); } catch { }
            return false;
        }
    }
}
