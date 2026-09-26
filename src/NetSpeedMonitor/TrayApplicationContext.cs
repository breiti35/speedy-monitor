using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace NetSpeedMonitor;

/// <summary>
/// Traegt die gesamte App: kein sichtbares Hauptfenster, nur das Overlay-Panel
/// direkt neben den System-Tray-Icons (Ersatz fuer das unter Windows 11 nicht mehr
/// moegliche Taskbar-Deskband). Das Panel ist die einzige Bedienoberflaeche:
/// Rechtsklick = Menue, Klick = Statistik-Flyout, Doppelklick = Einstellungen.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NetworkMonitor _monitor;
    private readonly StatsStore _stats;
    private readonly TaskbarOverlayWindow _overlay;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _anchorRefreshTimer;
    private readonly Queue<(double Up, double Down)> _history = new();
    private AppSettings _settings;
    private int _ticksSinceSave;
    private NetworkSample _lastSample;

    private StatsFlyout? _flyout;
    private SettingsForm? _settingsForm;
    private AboutDialog? _aboutDialog;

    private SpeedHeaderItem _headerItem = null!;
    private ToolStripMenuItem _unitMenu = null!;
    private ToolStripMenuItem _adapterMenu = null!;
    private ToolStripMenuItem _autostartItem = null!;

    private Rectangle _taskbarRect;
    private int? _notificationAreaLeft;

    private const int SaveEveryNTicks = 30;
    private const int AnchorRefreshMs = 5000;

    private static readonly (SpeedUnit Unit, string Text)[] UnitChoices =
    {
        (SpeedUnit.Auto, "Automatisch"),
        (SpeedUnit.KBs, "KB/s"),
        (SpeedUnit.MBs, "MB/s"),
        (SpeedUnit.Kbits, "kbit/s"),
        (SpeedUnit.Mbits, "Mbit/s")
    };

    public TrayApplicationContext()
    {
        _settings = SettingsStore.Load();
        _stats = new StatsStore();

        _menu = BuildContextMenu();
        MenuRenderer.Apply(_menu, ThemeHelper.GetAppPalette());

        _overlay = new TaskbarOverlayWindow(_menu);

        // Das Overlay meldet PanelClicked bereits erst nach Ablauf der Doppelklickzeit.
        _overlay.PanelClicked += (_, _) =>
        {
            if (!_menu.Visible)
                ToggleFlyout();
        };
        _overlay.PanelDoubleClicked += (_, _) => OpenSettings();

        // Das Panel ist die einzige Bedienoberflaeche - ShowTaskbarOverlay wird daher ignoriert.
        RefreshAnchor();
        _overlay.Reposition(_taskbarRect, _notificationAreaLeft);
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
            _overlay.Reposition(_taskbarRect, _notificationAreaLeft);
        };
        _anchorRefreshTimer.Start();

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Application.ApplicationExit += OnApplicationExit;
    }

    private void RefreshAnchor()
    {
        _taskbarRect = TaskbarLayoutHelper.GetTaskbarRect() ?? _taskbarRect;
        _notificationAreaLeft = TaskbarLayoutHelper.GetNotificationAreaLeft(_taskbarRect);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        _headerItem = new SpeedHeaderItem();
        _headerItem.SetValues(SpeedFormatter.FormatFull(0, _settings.Unit), SpeedFormatter.FormatFull(0, _settings.Unit));
        menu.Items.Add(_headerItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Statistik anzeigen…", null, (_, _) => ShowFlyout());

        _unitMenu = new ToolStripMenuItem("Einheit");
        foreach (var (unit, text) in UnitChoices)
            _unitMenu.DropDownItems.Add(new RadioMenuItem(text, (_, _) => SetUnit(unit)) { Tag = unit });
        menu.Items.Add(_unitMenu);

        _adapterMenu = new ToolStripMenuItem("Netzwerkadapter");
        // Platzhalter, damit der Untermenue-Pfeil erscheint; echte Eintraege beim Oeffnen.
        _adapterMenu.DropDownItems.Add("…");
        menu.Items.Add(_adapterMenu);

        _autostartItem = new ToolStripMenuItem("Mit Windows starten", null, (_, _) => ToggleAutostart());
        menu.Items.Add(_autostartItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Einstellungen…", null, (_, _) => OpenSettings());
        menu.Items.Add("Statistik zurücksetzen…", null, (_, _) => ConfirmResetStats());
        menu.Items.Add("Über Speedy Monitor", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitApplication());

        menu.Opening += (_, _) => RefreshMenuState();
        return menu;
    }

    private void RefreshMenuState()
    {
        UpdateHeader();

        foreach (ToolStripItem item in _unitMenu.DropDownItems)
            if (item is ToolStripMenuItem mi)
                mi.Checked = mi.Tag is SpeedUnit u && u == _settings.Unit;

        RebuildAdapterMenu();
        _autostartItem.Checked = AutostartHelper.IsEnabled();
    }

    private void UpdateHeader() => _headerItem.SetValues(
        SpeedFormatter.FormatFull(_lastSample.UploadBytesPerSecond, _settings.Unit),
        SpeedFormatter.FormatFull(_lastSample.DownloadBytesPerSecond, _settings.Unit));

    private void RebuildAdapterMenu()
    {
        var items = _adapterMenu.DropDownItems;
        items.Clear();
        items.Add(new RadioMenuItem("Automatisch (alle aktiven)", (_, _) => SetAdapter("Auto"))
        {
            Checked = _settings.AdapterId == "Auto"
        });

        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            interfaces = Array.Empty<NetworkInterface>();
        }

        var candidates = interfaces
            .Where(ni => ni.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .OrderByDescending(ni => ni.OperationalStatus == OperationalStatus.Up)
            .ThenBy(ni => ni.Name)
            .ToList();

        if (candidates.Count > 0)
            items.Add(new ToolStripSeparator());

        foreach (var ni in candidates)
        {
            var active = ni.OperationalStatus == OperationalStatus.Up;
            var id = ni.Id;
            items.Add(new RadioMenuItem(active ? ni.Name : $"{ni.Name} (inaktiv)", (_, _) => SetAdapter(id))
            {
                Checked = _settings.AdapterId == id,
                Enabled = active,
                ToolTipText = ni.Description
            });
        }

        if (_adapterMenu.DropDown is ToolStripDropDownMenu dd)
            MenuRenderer.Apply(dd, ThemeHelper.GetAppPalette());
    }

    private void SetUnit(SpeedUnit unit)
    {
        _settings.Unit = unit;
        SettingsStore.Save(_settings);
        PushValues();
    }

    private void SetAdapter(string adapterId)
    {
        if (_settings.AdapterId == adapterId)
            return;
        _settings.AdapterId = adapterId;
        SettingsStore.Save(_settings);
        _monitor.AdapterId = adapterId;
        _monitor.ResetBaseline();
        _history.Clear();
    }

    private void ToggleAutostart()
    {
        _settings.AutostartEnabled = !AutostartHelper.IsEnabled();
        AutostartHelper.SetEnabled(_settings.AutostartEnabled);
        SettingsStore.Save(_settings);
    }

    private void OnSample(NetworkSample sample)
    {
        _stats.AddSample(sample.UploadBytesDelta, sample.DownloadBytesDelta);
        _lastSample = sample;

        _history.Enqueue((sample.UploadBytesPerSecond, sample.DownloadBytesPerSecond));
        while (_history.Count > StatsFlyout.HistoryLength)
            _history.Dequeue();

        PushValues();
        if (_menu.Visible)
            UpdateHeader();

        if (++_ticksSinceSave >= SaveEveryNTicks)
        {
            _ticksSinceSave = 0;
            _stats.Save();
        }
    }

    private void PushValues()
    {
        var up = _lastSample.UploadBytesPerSecond;
        var down = _lastSample.DownloadBytesPerSecond;
        var flyoutOpen = _flyout is { Visible: true };
        // Bei offenem Flyout/Menue wuerde der Tooltip sich darueber legen - die Werte stehen dort ohnehin.
        var hideTooltip = flyoutOpen || _menu.Visible;
        _overlay.UpdateValues(up, down, _settings.Unit, hideTooltip ? string.Empty : BuildTooltip());
        if (flyoutOpen)
            _flyout!.UpdateValues(_settings.Unit, up, down);
    }

    private string BuildTooltip()
    {
        var up = SpeedFormatter.FormatFull(_lastSample.UploadBytesPerSecond, _settings.Unit);
        var down = SpeedFormatter.FormatFull(_lastSample.DownloadBytesPerSecond, _settings.Unit);
        var today = _stats.Today;
        return $"↑ {up}   ↓ {down}\n"
               + $"Heute: ↑ {ByteFormatter.FormatBytes(today.UploadBytes)}   ↓ {ByteFormatter.FormatBytes(today.DownloadBytes)}";
    }

    private void ToggleFlyout()
    {
        // Ein Klick aufs Panel bei offenem Flyout kann es bereits per Fokusverlust geschlossen
        // haben - dann soll derselbe Klick es nicht sofort wieder oeffnen.
        if (_flyout is { Visible: false }
            && (DateTime.UtcNow - _flyout.LastDeactivatedUtc).TotalMilliseconds < SystemInformation.DoubleClickTime + 300)
            return;

        if (_flyout is { Visible: true })
            _flyout.Dismiss();
        else
            ShowFlyout();
    }

    private void ShowFlyout()
    {
        _flyout ??= new StatsFlyout(_stats, _monitor, _history);
        _flyout.ShowNear(_overlay.Bounds, _settings.Unit, _lastSample.UploadBytesPerSecond, _lastSample.DownloadBytesPerSecond);
    }

    private void OpenSettings()
    {
        _flyout?.Dismiss();
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.WindowState = FormWindowState.Normal;
            WindowActivation.ForceForeground(_settingsForm);
            return;
        }

        var form = new SettingsForm(_settings);
        form.FormClosed += (_, _) =>
        {
            _settingsForm = null;
            if (form.DialogResult == DialogResult.OK)
                ApplySettings(form.Result);
        };
        _settingsForm = form;
        form.Show();
        WindowActivation.ForceForeground(form);
    }

    private void ApplySettings(AppSettings result)
    {
        var adapterChanged = result.AdapterId != _settings.AdapterId;

        _settings = result;
        SettingsStore.Save(_settings);

        _monitor.AdapterId = _settings.AdapterId;
        _monitor.IntervalMs = _settings.UpdateIntervalMs;
        if (adapterChanged)
        {
            _monitor.ResetBaseline();
            _history.Clear();
        }

        AutostartHelper.SetEnabled(_settings.AutostartEnabled);
        PushValues();
    }

    private void ConfirmResetStats()
    {
        var confirmed = ConfirmDialog.Ask(
            "Speedy Monitor",
            "Statistik zurücksetzen?",
            "Alle gespeicherten Datenmengen (Sitzung, Tage, Woche und Monat) werden unwiderruflich gelöscht.",
            "Zurücksetzen");
        if (confirmed)
            _stats.Reset();
    }

    private void ShowAbout()
    {
        if (_aboutDialog is { IsDisposed: false })
        {
            WindowActivation.ForceForeground(_aboutDialog);
            return;
        }
        _aboutDialog = new AboutDialog();
        _aboutDialog.FormClosed += (_, _) => _aboutDialog = null;
        _aboutDialog.Show();
        WindowActivation.ForceForeground(_aboutDialog);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
            return;

        var palette = ThemeHelper.GetAppPalette();
        MenuRenderer.Apply(_menu, palette);
        _flyout?.ApplyPalette(palette);
    }

    private void OnApplicationExit(object? sender, EventArgs e) => _stats.Save();

    private void ExitApplication()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Application.ApplicationExit -= OnApplicationExit;
        _stats.Save();
        _anchorRefreshTimer.Dispose();
        _monitor.Dispose();
        _settingsForm?.Close();
        _aboutDialog?.Close();
        _flyout?.Dispose();
        _overlay.Dispose();
        _menu.Dispose();
        Application.Exit();
    }
}
