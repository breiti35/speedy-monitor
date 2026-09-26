using System.Text;

namespace NetSpeedMonitor;

/// <summary>
/// Traegt die gesamte App: kein sichtbares Hauptfenster, nur das Overlay-Panel
/// direkt neben den System-Tray-Icons (Ersatz fuer das unter Windows 11 nicht mehr
/// moegliche Taskbar-Deskband). Das Panel selbst stellt auch das Rechtsklick-Menue
/// (Einstellungen/Beenden) und den Doppelklick-Zugriff auf die Einstellungen bereit.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NetworkMonitor _monitor;
    private readonly StatsStore _stats;
    private readonly TaskbarOverlayWindow _overlay;
    private readonly System.Windows.Forms.Timer _anchorRefreshTimer;
    private AppSettings _settings;
    private int _ticksSinceSave;

    private Rectangle _taskbarRect;
    private int? _notificationAreaLeft;

    private const int SaveEveryNTicks = 30;
    private const int AnchorRefreshMs = 5000;

    public TrayApplicationContext()
    {
        _settings = SettingsStore.Load();
        _stats = new StatsStore();

        var menu = BuildContextMenu();

        _overlay = new TaskbarOverlayWindow(menu);
        _overlay.PanelDoubleClicked += (_, _) => OpenSettings();

        RefreshAnchor();
        _overlay.Reposition(_taskbarRect, _notificationAreaLeft);
        _overlay.Visible = _settings.ShowTaskbarOverlay;
        if (_settings.ShowTaskbarOverlay)
            _overlay.Show();

        _monitor = new NetworkMonitor
        {
            AdapterId = _settings.AdapterId,
            IntervalMs = _settings.UpdateIntervalMs
        };
        _monitor.SampleReady += OnSample;
        _monitor.Start();

        if (_settings.AutostartEnabled != AutostartHelper.IsEnabled())
            AutostartHelper.SetEnabled(_settings.AutostartEnabled);

        // Taskleisten-/Tray-Position ist normalerweise stabil, kann sich aber durch
        // Aufloesungswechsel, Monitorwechsel oder einen Explorer-Neustart aendern.
        _anchorRefreshTimer = new System.Windows.Forms.Timer { Interval = AnchorRefreshMs };
        _anchorRefreshTimer.Tick += (_, _) =>
        {
            RefreshAnchor();
            if (_overlay.Visible)
                _overlay.Reposition(_taskbarRect, _notificationAreaLeft);
        };
        _anchorRefreshTimer.Start();

        Application.ApplicationExit += (_, _) => _stats.Save();
    }

    private void RefreshAnchor()
    {
        _taskbarRect = TaskbarLayoutHelper.GetTaskbarRect() ?? _taskbarRect;
        _notificationAreaLeft = TaskbarLayoutHelper.GetNotificationAreaLeft(_taskbarRect);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Einstellungen…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitApplication());
        return menu;
    }

    private void OnSample(NetworkSample sample)
    {
        _stats.AddSample(sample.UploadBytesDelta, sample.DownloadBytesDelta);

        var tooltip = BuildTooltip(sample);

        if (_settings.ShowTaskbarOverlay)
            _overlay.UpdateValues(sample.UploadBytesPerSecond, sample.DownloadBytesPerSecond, _settings.Unit, tooltip);

        if (++_ticksSinceSave >= SaveEveryNTicks)
        {
            _ticksSinceSave = 0;
            _stats.Save();
        }
    }

    private string BuildTooltip(NetworkSample sample)
    {
        var up = SpeedFormatter.FormatFull(sample.UploadBytesPerSecond, _settings.Unit);
        var down = SpeedFormatter.FormatFull(sample.DownloadBytesPerSecond, _settings.Unit);
        var today = _stats.Today;
        var week = _stats.WeekTotal();

        var sb = new StringBuilder();
        sb.Append("NetSpeed Monitor\n");
        sb.Append($"↑ {up}   ↓ {down}\n");
        sb.Append($"Heute: ↑{ByteFormatter.FormatBytes(today.UploadBytes)} ↓{ByteFormatter.FormatBytes(today.DownloadBytes)}\n");
        sb.Append($"Woche: ↑{ByteFormatter.FormatBytes(week.Upload)} ↓{ByteFormatter.FormatBytes(week.Download)}");

        return sb.ToString();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK)
            return;

        var adapterChanged = form.Result.AdapterId != _settings.AdapterId;
        var overlayChanged = form.Result.ShowTaskbarOverlay != _settings.ShowTaskbarOverlay;

        _settings = form.Result;
        SettingsStore.Save(_settings);

        _monitor.AdapterId = _settings.AdapterId;
        _monitor.IntervalMs = _settings.UpdateIntervalMs;
        if (adapterChanged)
            _monitor.ResetBaseline();

        if (overlayChanged)
        {
            if (_settings.ShowTaskbarOverlay)
            {
                RefreshAnchor();
                _overlay.Reposition(_taskbarRect, _notificationAreaLeft);
                _overlay.Show();
            }
            else
            {
                _overlay.Hide();
            }
        }

        AutostartHelper.SetEnabled(_settings.AutostartEnabled);
    }

    private void ExitApplication()
    {
        _stats.Save();
        _anchorRefreshTimer.Dispose();
        _monitor.Dispose();
        _overlay.Dispose();
        Application.Exit();
    }
}
