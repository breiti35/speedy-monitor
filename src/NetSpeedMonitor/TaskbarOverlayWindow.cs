using Microsoft.Win32;
using static NetSpeedMonitor.OverlayNative;

namespace NetSpeedMonitor;

/// <summary>
/// Rahmenloses, immer im Vordergrund liegendes Panel, das sich direkt links neben die
/// System-Tray-Icons andockt und Upload/Download anzeigt - der bestmoegliche Ersatz fuer
/// das klassische Taskbar-Deskband, das es unter Windows 11 nicht mehr gibt.
/// Technisch ein separates, nicht aktivierbares Layered Window mit Per-Pixel-Alpha: der
/// Text wird direkt "auf" die Taskleiste gezeichnet, ohne sichtbaren Hintergrundkasten.
///
/// Groesse und Schriftgroesse sind FEST (nur von der Taskleistenhoehe abhaengig) und
/// aendern sich bei Wertewechseln bewusst nicht - so springt/verschiebt sich nichts.
/// </summary>
public sealed class TaskbarOverlayWindow : Form
{
    private const int GapToTray = 6;
    private const int FallbackOffsetFromRight = 170;
    private const int AutoHidePollMs = 150;

    // Shell-Fenster, die zwar bildschirmfuellend sein koennen, aber keine Vollbild-App sind
    // (Desktop, Taskleiste, Start/Suche, Aufgabenansicht).
    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow"
    };

    private readonly ToolTip _toolTip = new();
    private string? _toolTipText;

    private OverlayRenderer? _renderer;
    private OverlaySurface? _surface;
    private OverlayContent _content = new("0,0", "KB/s", "0,0", "KB/s");
    private bool _lightTheme;
    private bool _dirty = true;
    private bool _presentPending = true;
    private Point _presentedLocation;
    private byte _presentedAlpha;

    private Rectangle _taskbarRect;
    private int? _notificationAreaLeft;
    private bool _suppressedFullscreen;
    private bool _suppressedAutoHide;

    private IntPtr _winEventHook;
    private WinEventProc? _winEventProc;
    private readonly System.Windows.Forms.Timer _burstTimer;
    private int _burstRemaining;
    private readonly System.Windows.Forms.Timer _clickTimer;
    private readonly System.Windows.Forms.Timer _autoHidePollTimer;

    public event EventHandler? PanelClicked;
    public event EventHandler? PanelDoubleClicked;

    public TaskbarOverlayWindow(ContextMenuStrip contextMenu)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ContextMenuStrip = contextMenu;

        _lightTheme = ThemeHelper.IsSystemLightTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // Einzelklick erst nach Ablauf der Doppelklickzeit melden, damit ein Doppelklick
        // (Einstellungen) nicht zusaetzlich noch das Statistik-Flyout oeffnet.
        _clickTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, SystemInformation.DoubleClickTime) };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            PanelClicked?.Invoke(this, EventArgs.Empty);
        };
        MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;
            _clickTimer.Stop();
            _clickTimer.Start();
        };
        MouseDoubleClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;
            _clickTimer.Stop();
            PanelDoubleClicked?.Invoke(this, EventArgs.Empty);
        };

        _burstTimer = new System.Windows.Forms.Timer { Interval = 60 };
        _burstTimer.Tick += (_, _) =>
        {
            ReassertTopmost();
            if (--_burstRemaining <= 0)
                _burstTimer.Stop();
        };

        // Eine automatisch ausgeblendete Taskleiste faehrt jederzeit ein/aus - der 1s-Tick
        // waere dafuer zu traege. Der Timer laeuft nur, solange Auto-Hide aktiv ist.
        _autoHidePollTimer = new System.Windows.Forms.Timer { Interval = AutoHidePollMs };
        _autoHidePollTimer.Tick += (_, _) => RefreshTaskbarGeometry();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_LAYERED = 0x00080000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Reagiert auf jeden Vordergrundwechsel im System (Start-Menue oeffnen/schliessen,
        // Taskleiste anklicken, Alt+Tab, ...) und holt das Panel dann sofort per Reassert-Salve
        // zurueck nach vorne, statt bis zum naechsten periodischen Tick zu warten - sonst bleibt
        // das Panel nach einem Klick auf Start laengere Zeit verdeckt.
        _winEventProc = OnForegroundChanged;
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        _presentPending = true;
        Present();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnhookForeground();
        base.OnHandleDestroyed(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            _presentPending = true;
            RefreshTaskbarGeometry();
            EvaluateFullscreen();
            Present();
            if (!IsSuppressed)
                ReassertTopmost();
        }
    }

    private void UnhookForeground()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        EvaluateFullscreen();
        Present();
        if (IsSuppressed)
        {
            _burstTimer.Stop();
            return;
        }

        // Sofort einmal, plus eine kurze Salve (mehrere Wiederholungen im 60ms-Abstand), da der
        // Shell-Vorgang selbst manchmal knapp NACH unserem ersten Reassert nochmal seine eigene
        // Topmost-Position setzt - eine einzelne Reaktion wuerde das Rennen dann verlieren.
        ReassertTopmost();
        _burstRemaining = 8;
        _burstTimer.Start();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            BeginInvoke(() => OnUserPreferenceChanged(sender, e));
            return;
        }
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        var light = ThemeHelper.IsSystemLightTheme();
        if (light == _lightTheme)
            return;
        _lightTheme = light;
        _dirty = true;
        Present();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            UnhookForeground();
            _burstTimer.Dispose();
            _clickTimer.Dispose();
            _autoHidePollTimer.Dispose();
            _toolTip.Dispose();
            _surface?.Dispose();
            _surface = null;
            _renderer?.Dispose();
            _renderer = null;
        }
        base.Dispose(disposing);
    }

    public void UpdateValues(double uploadBytesPerSecond, double downloadBytesPerSecond, SpeedUnit unit, string tooltipText)
    {
        var (upValue, upUnit) = SpeedFormatter.FormatParts(uploadBytesPerSecond, unit);
        var (downValue, downUnit) = SpeedFormatter.FormatParts(downloadBytesPerSecond, unit);
        var content = new OverlayContent(upValue, upUnit, downValue, downUnit);
        if (content != _content)
        {
            _content = content;
            _dirty = true;
        }

        if (tooltipText != _toolTipText)
        {
            _toolTipText = tooltipText;
            _toolTip.SetToolTip(this, tooltipText);
        }

        RefreshTaskbarGeometry();
        EvaluateFullscreen();
        Present();

        if (!IsSuppressed)
            ReassertTopmost();
    }

    /// <summary>
    /// Dockt das Panel links neben den Tray-Cluster an. Schrift und Panelgroesse werden nur
    /// neu berechnet, wenn sich die Taskleistenhoehe tatsaechlich geaendert hat.
    /// </summary>
    public void Reposition(Rectangle taskbarRect, int? notificationAreaLeft)
    {
        _notificationAreaLeft = notificationAreaLeft;
        ApplyTaskbarRect(taskbarRect);

        var autoHide = TaskbarLayoutHelper.IsAutoHideEnabled();
        if (autoHide != _autoHidePollTimer.Enabled)
            _autoHidePollTimer.Enabled = autoHide;

        EvaluateFullscreen();
        Present();
        if (!IsSuppressed)
            ReassertTopmost();
    }

    private bool IsSuppressed => _suppressedFullscreen || _suppressedAutoHide;

    private void RefreshTaskbarGeometry()
    {
        var rect = TaskbarLayoutHelper.GetTaskbarRect();
        if (rect is { } r && r != _taskbarRect)
        {
            var wasSuppressed = IsSuppressed;
            ApplyTaskbarRect(r);
            Present();
            // Eingefahrene Auto-Hide-Taskleiste legt sich beim Einblenden ueber uns.
            if (wasSuppressed && !IsSuppressed)
                ReassertTopmost();
        }
    }

    private void ApplyTaskbarRect(Rectangle taskbarRect)
    {
        if (taskbarRect.Width <= 0 || taskbarRect.Height <= 0)
            return;

        _taskbarRect = taskbarRect;

        if (_renderer is null || _renderer.Height != taskbarRect.Height)
        {
            _surface?.Dispose();
            _surface = null;
            _renderer?.Dispose();
            _renderer = new OverlayRenderer(taskbarRect.Height);
            _surface = new OverlaySurface(_renderer.Width, _renderer.Height);
            _dirty = true;
            _presentPending = true;
        }

        var right = (_notificationAreaLeft ?? taskbarRect.Right - FallbackOffsetFromRight) - GapToTray;
        var bounds = new Rectangle(right - _renderer.Width, taskbarRect.Top, _renderer.Width, _renderer.Height);
        if (Bounds != bounds)
            Bounds = bounds;

        // Auto-Hide: eingefahrene Taskleiste ragt nur noch wenige Pixel ins Bild - dann nicht
        // allein ueber dem Desktop schweben.
        var monitor = TaskbarMonitorBounds();
        var visibleHeight = Rectangle.Intersect(taskbarRect, monitor).Height;
        _suppressedAutoHide = visibleHeight < 10 || visibleHeight < taskbarRect.Height * 0.9;
    }

    private Rectangle TaskbarMonitorBounds()
    {
        // Oberkante statt Mitte: bei eingefahrener Taskleiste liegt nur dieser Streifen noch
        // auf "ihrem" Monitor.
        var probe = new Point(_taskbarRect.Left + _taskbarRect.Width / 2, _taskbarRect.Top);
        return Screen.FromPoint(probe).Bounds;
    }

    private void EvaluateFullscreen()
    {
        _suppressedFullscreen = IsHandleCreated && !_taskbarRect.IsEmpty && IsForegroundFullscreen();
    }

    private bool IsForegroundFullscreen()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == Handle)
            return false;

        GetWindowThreadProcessId(fg, out var ownerProcess);
        if (ownerProcess == (uint)Environment.ProcessId)
            return false;

        if (ShellWindowClasses.Contains(GetWindowClass(fg)))
            return false;

        if (!GetWindowRect(fg, out var r))
            return false;

        var monitor = TaskbarMonitorBounds();
        return r.Left <= monitor.Left && r.Top <= monitor.Top
            && r.Right >= monitor.Right && r.Bottom >= monitor.Bottom;
    }

    /// <summary>
    /// Zeichnet nur bei geaendertem Text/Theme/Groesse neu; UpdateLayeredWindow selbst nur,
    /// wenn sich Inhalt, Position oder Sichtbarkeit (Alpha) geaendert haben.
    /// Unterdrueckt wird ueber Alpha 0 statt Hide(), weil Visible der App gehoert.
    /// </summary>
    private void Present()
    {
        if (!IsHandleCreated || _renderer is null || _surface is null)
            return;

        if (_dirty)
        {
            using (var g = Graphics.FromImage(_surface.Bitmap))
                _renderer.Render(g, _content, _lightTheme);
            _dirty = false;
            _presentPending = true;
        }

        var alpha = IsSuppressed ? (byte)0 : (byte)255;
        var location = Location;
        if (!_presentPending && location == _presentedLocation && alpha == _presentedAlpha)
            return;

        if (_surface.Present(Handle, location, alpha))
        {
            _presentPending = false;
            _presentedLocation = location;
            _presentedAlpha = alpha;
        }
    }

    /// <summary>
    /// Windows 11 rendert die Taskleiste selbst mit einer erhoehten Shell-Prioritaet und
    /// stellt sich bei diversen eigenen Ereignissen (Hover, Klicks, Icon-Updates) immer
    /// wieder ueber ganz normale TopMost-Fenster - unser Panel wird dadurch sonst unsichtbar
    /// dahinter verdeckt. Deshalb bei jeder Aktualisierung aktiv erneut ganz nach vorne holen.
    /// </summary>
    private void ReassertTopmost()
    {
        if (!IsHandleCreated || IsSuppressed)
            return;

        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        var HWND_TOPMOST = new IntPtr(-1);

        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
