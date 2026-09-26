using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace NetSpeedMonitor;

/// <summary>Eine Taskleiste samt X-Koordinate, an der die rechte Panelkante andocken soll.</summary>
public sealed record TaskbarInfo(IntPtr Handle, Rectangle Bounds, int? AnchorRight, bool IsPrimary);

/// <summary>
/// Ermittelt Lage und Ausdehnung der Taskleisten sowie die linke Kante des
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
    private const string PrimaryClass = "Shell_TrayWnd";
    private const string SecondaryClass = "Shell_SecondaryTrayWnd";
    private const string TrayIconAutomationId = "SystemTrayIcon";

    /// <summary>
    /// Alle horizontalen Taskleisten, die primaere zuerst. Leer, solange der Explorer
    /// (neu) startet.
    /// </summary>
    public static List<TaskbarInfo> GetTaskbars(bool includeSecondary)
    {
        var result = new List<TaskbarInfo>();

        var primary = FindWindow(PrimaryClass, null);
        if (GetWindowBounds(primary) is { } primaryRect && IsHorizontal(primaryRect))
            result.Add(new TaskbarInfo(primary, primaryRect, GetNotificationAreaLeft(primary, primaryRect), true));

        if (!includeSecondary)
            return result;

        var hwnd = IntPtr.Zero;
        while ((hwnd = FindWindowEx(IntPtr.Zero, hwnd, SecondaryClass, null)) != IntPtr.Zero)
        {
            if (!IsWindowVisible(hwnd) || GetWindowBounds(hwnd) is not { } rect || !IsHorizontal(rect))
                continue;
            result.Add(new TaskbarInfo(hwnd, rect, GetSecondaryAnchor(hwnd, rect), false));
        }

        return result;
    }

    public static Rectangle? GetWindowBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r))
            return null;
        var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        return rect.Width > 0 && rect.Height > 0 ? rect : null;
    }

    // Senkrechte Taskleisten gibt es unter Windows 11 nicht mehr - das Panel ist nur waagerecht ausgelegt.
    private static bool IsHorizontal(Rectangle rect) => rect.Width > rect.Height;

    /// <summary>
    /// Liefert die linke Bildschirm-X-Koordinate des Tray-Icon-Clusters, oder null,
    /// falls sie sich gerade nicht ermitteln laesst (z.B. waehrend eines Explorer-Neustarts).
    /// </summary>
    private static int? GetNotificationAreaLeft(IntPtr hwnd, Rectangle taskbarRect)
    {
        try
        {
            var trayElement = AutomationElement.FromHandle(hwnd);
            var children = trayElement.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);

            int? best = null;
            foreach (AutomationElement child in children)
            {
                if (!TryGetBounds(child, out var rect))
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

    /// <summary>
    /// Nebentaskleisten haben keinen Tray-Bereich; rechts steht hoechstens die Uhr (falls
    /// "Uhr auf allen Taskleisten" aktiv ist). Sie haengt - anders als auf der Haupttaskleiste -
    /// nicht als eigenes Kind-Element am Fenster, sondern als "SystemTrayIcon"-Button im
    /// XAML-Island eine Ebene tiefer. Ohne Uhr dockt das Panel am rechten Rand an.
    /// </summary>
    private static int? GetSecondaryAnchor(IntPtr hwnd, Rectangle taskbarRect)
    {
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            var trayIcon = new PropertyCondition(AutomationElement.AutomationIdProperty, TrayIconAutomationId);

            int? best = null;
            foreach (AutomationElement child in root.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition))
            {
                foreach (AutomationElement icon in child.FindAll(TreeScope.Children, trayIcon))
                {
                    if (!TryGetBounds(icon, out var rect) || rect.Left < taskbarRect.Left + taskbarRect.Width * 0.5)
                        continue;
                    if (best is null || rect.Left < best)
                        best = (int)rect.Left;
                }
            }

            return best ?? taskbarRect.Right - Math.Max(4, taskbarRect.Height / 8);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetBounds(AutomationElement element, out Rect rect)
    {
        try { rect = element.Current.BoundingRectangle; }
        catch
        {
            rect = Rect.Empty;
            return false;
        }
        return !rect.IsEmpty && !double.IsInfinity(rect.Width);
    }

    /// <summary>"Taskleiste automatisch ausblenden" aktiv?</summary>
    public static bool IsAutoHideEnabled()
    {
        const uint ABM_GETSTATE = 0x04;
        const int ABS_AUTOHIDE = 0x01;
        try
        {
            var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
            return ((int)SHAppBarMessage(ABM_GETSTATE, ref data) & ABS_AUTOHIDE) != 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

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
