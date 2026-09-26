using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace NetSpeedMonitor;

/// <summary>
/// Ermittelt Lage und Ausdehnung der Taskleiste sowie die linke Kante des
/// Systemsteuerungsbereichs (Netzwerk/Lautstaerke/Uhr/Tray-Icons), damit das
/// Overlay-Panel direkt links daneben andocken kann.
///
/// Windows 11 rendert die Taskleiste ueber ein XAML-Island ohne klassische
/// Kind-Fenster (kein "TrayNotifyWnd" mehr per FindWindowEx erreichbar) - die
/// Position des Tray-Clusters wird deshalb per UI Automation ermittelt: der
/// Systemsteuerungsbereich ist ein eigenstaendiges, schmaleres Kind-Element von
/// Shell_TrayWnd am rechten Rand.
/// </summary>
public static class TaskbarLayoutHelper
{
    public static Rectangle? GetTaskbarRect()
    {
        var hwnd = FindWindow("Shell_TrayWnd", null);
        if (hwnd == IntPtr.Zero)
            return null;

        return GetWindowRect(hwnd, out var r)
            ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)
            : null;
    }

    /// <summary>
    /// Liefert die linke Bildschirm-X-Koordinate des Tray-Icon-Clusters, oder null,
    /// falls sie sich gerade nicht ermitteln laesst (z.B. waehrend eines Explorer-Neustarts).
    /// </summary>
    public static int? GetNotificationAreaLeft(Rectangle taskbarRect)
    {
        try
        {
            var hwnd = FindWindow("Shell_TrayWnd", null);
            if (hwnd == IntPtr.Zero)
                return null;

            var trayElement = AutomationElement.FromHandle(hwnd);
            var children = trayElement.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);

            int? best = null;
            foreach (AutomationElement child in children)
            {
                Rect rect;
                try { rect = child.Current.BoundingRectangle; }
                catch { continue; }

                if (rect.IsEmpty || double.IsInfinity(rect.Width))
                    continue;

                // Die volle Taskleisten-Flaeche selbst ueberspringen.
                if (rect.Width >= taskbarRect.Width * 0.9)
                    continue;

                // Nur Elemente in der rechten Haelfte beruecksichtigen (Tray-Bereich).
                if (rect.Left < taskbarRect.Left + taskbarRect.Width * 0.5)
                    continue;

                if (best is null || rect.Left < best)
                    best = (int)rect.Left;
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
