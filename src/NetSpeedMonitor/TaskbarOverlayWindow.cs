using System.Linq;
using System.Runtime.InteropServices;

namespace NetSpeedMonitor;

/// <summary>
/// Rahmenloses, immer im Vordergrund liegendes Panel, das sich direkt links neben die
/// System-Tray-Icons andockt und Upload/Download als Text anzeigt - der bestmoegliche
/// Ersatz fuer das klassische Taskbar-Deskband, das es unter Windows 11 nicht mehr gibt.
/// Es handelt sich technisch um ein separates, nicht aktivierbares Fenster, keine echte
/// Einbettung in die Taskleiste - siehe TaskbarLayoutHelper fuer die Positionierung.
///
/// Groesse und Schriftgroesse sind FEST (einmalig anhand der Taskleistenhoehe berechnet)
/// und werden bei Wertewechseln bewusst nicht mehr angepasst - so springt/verschiebt sich
/// das Panel nicht mehr, wenn Zahlen laenger oder kuerzer werden.
/// </summary>
public sealed class TaskbarOverlayWindow : Form
{
    private const int GapToTray = 6;
    private const int HorizontalPadding = 10;
    private const int FallbackOffsetFromRight = 170;

    // Deckt die realistische Bandbreite aller waehlbaren Einheiten ab, damit die feste
    // Panelbreite auch bei laengeren Werten (z.B. Mbit/s bei sehr schnellen Verbindungen)
    // nicht zu knapp bemessen ist.
    private static readonly string[] WidthTemplates =
    {
        "↑ 999,9 KB/s",
        "↑ 999,99 MB/s",
        "↑ 9999,9 kbit/s",
        "↑ 999,99 Mbit/s"
    };

    private static readonly Color UploadColor = Color.FromArgb(255, 152, 0);
    private static readonly Color DownloadColor = Color.FromArgb(0, 205, 120);

    private readonly Label _uploadLabel;
    private readonly Label _downloadLabel;
    private readonly ToolTip _toolTip = new();

    private Font? _currentFont;
    private int _lastTaskbarHeight = -1;

    private IntPtr _winEventHook;
    private WinEventProc? _winEventProc;
    private readonly System.Windows.Forms.Timer _burstTimer;
    private int _burstRemaining;

    public TaskbarOverlayWindow(ContextMenuStrip contextMenu)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ContextMenuStrip = contextMenu;

        _uploadLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            AutoEllipsis = true,
            ForeColor = UploadColor
        };
        _downloadLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
            AutoEllipsis = true,
            ForeColor = DownloadColor
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(HorizontalPadding, 0, HorizontalPadding, 0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.Controls.Add(_uploadLabel, 0, 0);
        layout.Controls.Add(_downloadLabel, 0, 1);
        Controls.Add(layout);

        ApplyTheme();

        foreach (Control c in new Control[] { this, layout, _uploadLabel, _downloadLabel })
        {
            c.Click += (_, _) => PanelClicked?.Invoke(this, EventArgs.Empty);
            c.DoubleClick += (_, _) => PanelDoubleClicked?.Invoke(this, EventArgs.Empty);
        }

        _burstTimer = new System.Windows.Forms.Timer { Interval = 60 };
        _burstTimer.Tick += (_, _) =>
        {
            ReassertTopmost();
            if (--_burstRemaining <= 0)
                _burstTimer.Stop();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Reagiert auf jeden Vordergrundwechsel im System (Start-Menue oeffnen/schliessen,
        // Taskleiste anklicken, Alt+Tab, ...) und holt das Panel dann sofort per Reassert-Salve
        // zurueck nach vorne, statt bis zum naechsten periodischen Tick zu warten - genau das
        // fuehrt sonst dazu, dass das Panel nach einem Klick auf Start laengere Zeit verdeckt
        // bleibt, bis irgendein anderes Ereignis (z.B. Klick auf den Desktop) es zufaellig wieder
        // nach vorne holt.
        _winEventProc = OnForegroundChanged;
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_winEventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
        base.OnHandleDestroyed(e);
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Sofort einmal, plus eine kurze Salve (mehrere Wiederholungen im 60ms-Abstand), da der
        // Shell-Vorgang selbst manchmal knapp NACH unserem ersten Reassert nochmal seine eigene
        // Topmost-Position setzt - eine einzelne Reaktion wuerde das Rennen dann verlieren.
        ReassertTopmost();
        _burstRemaining = 8;
        _burstTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
            _burstTimer.Dispose();
            _currentFont?.Dispose();
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    public void ApplyTheme()
    {
        var isLight = IsSystemLightTheme();
        var back = isLight ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);

        BackColor = back;
        _uploadLabel.BackColor = back;
        _downloadLabel.BackColor = back;
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Aktualisiert nur den angezeigten Text - Groesse/Position bleiben unangetastet,
    /// damit unterschiedlich lange Werte das Panel nicht verschieben.
    /// </summary>
    public event EventHandler? PanelClicked;
    public event EventHandler? PanelDoubleClicked;

    public void UpdateValues(double uploadBytesPerSecond, double downloadBytesPerSecond, SpeedUnit unit, string tooltipText)
    {
        _uploadLabel.Text = "↑ " + SpeedFormatter.FormatFull(uploadBytesPerSecond, unit);
        _downloadLabel.Text = "↓ " + SpeedFormatter.FormatFull(downloadBytesPerSecond, unit);

        _toolTip.SetToolTip(_uploadLabel, tooltipText);
        _toolTip.SetToolTip(_downloadLabel, tooltipText);

        ReassertTopmost();
    }

    /// <summary>
    /// Windows 11 rendert die Taskleiste selbst mit einer erhoehten Shell-Prioritaet und
    /// stellt sich bei diversen eigenen Ereignissen (Hover, Klicks, Icon-Updates) immer
    /// wieder ueber ganz normale TopMost-Fenster - unser Panel wird dadurch sonst unsichtbar
    /// dahinter verdeckt. Deshalb bei jeder Aktualisierung aktiv erneut ganz nach vorne holen.
    /// </summary>
    private void ReassertTopmost()
    {
        if (!IsHandleCreated)
            return;

        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        var HWND_TOPMOST = new IntPtr(-1);

        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    /// <summary>
    /// Dockt das Panel links neben den Tray-Cluster an. Berechnet Schriftgroesse und feste
    /// Panelbreite nur neu, wenn sich die Taskleistenhoehe tatsaechlich geaendert hat
    /// (Aufloesung/Monitor/Skalierung) - im Normalbetrieb bleibt die Groesse konstant.
    /// </summary>
    public void Reposition(Rectangle taskbarRect, int? notificationAreaLeft)
    {
        if (taskbarRect.Height > 0 && taskbarRect.Height != _lastTaskbarHeight)
        {
            _lastTaskbarHeight = taskbarRect.Height;
            ApplySizing(taskbarRect.Height);
        }

        var right = (notificationAreaLeft ?? taskbarRect.Right - FallbackOffsetFromRight) - GapToTray;
        var left = right - Width;

        Bounds = new Rectangle(left, taskbarRect.Top, Width, taskbarRect.Height);
        ReassertTopmost();
    }

    private void ApplySizing(int taskbarHeight)
    {
        // Grosszuegige, deutlich besser lesbare Schrift statt der vorherigen Mini-Schrift -
        // orientiert sich an der halben Zeilenhoehe je Textzeile.
        var fontPixelSize = Math.Clamp(taskbarHeight / 2f * 0.62f, 12f, 22f);
        var newFont = new Font("Segoe UI", fontPixelSize, FontStyle.Bold, GraphicsUnit.Pixel);

        _uploadLabel.Font = newFont;
        _downloadLabel.Font = newFont;
        _currentFont?.Dispose();
        _currentFont = newFont;

        var templateWidth = WidthTemplates.Max(t => TextRenderer.MeasureText(t, newFont).Width);
        Width = templateWidth + HorizontalPadding * 2 + 4;
        Height = taskbarHeight;
    }
}
