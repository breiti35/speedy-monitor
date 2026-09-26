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
    // Primaere Taskleiste zuerst; weitere nur bei "Auf allen Monitoren anzeigen".
    private List<TaskbarOverlayWindow> _overlays = new();
    private ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _anchorRefreshTimer;
    private readonly Queue<(double Up, double Down)> _history = new();
    private AppSettings _settings;
    private int _ticksSinceSave;
    private NetworkSample _lastSample;

    private StatsFlyout? _flyout;
    private TaskbarOverlayWindow? _flyoutOwner;
    private SettingsForm? _settingsForm;
    private AboutDialog? _aboutDialog;

    private SpeedHeaderItem _headerItem = null!;
    private ToolStripMenuItem _unitMenu = null!;
    private ToolStripMenuItem _adapterMenu = null!;
    private ToolStripMenuItem _autostartItem = null!;
    private ToolStripMenuItem _allMonitorsItem = null!;
    private readonly SynchronizationContext? _uiContext;

    private const int SaveEveryNTicks = 30;
    private const int AnchorRefreshMs = 5000;

    private static (SpeedUnit Unit, string Text)[] UnitChoices =>
    [
        (SpeedUnit.Auto, L.Automatic),
        (SpeedUnit.KBs, "KB/s"),
        (SpeedUnit.MBs, "MB/s"),
        (SpeedUnit.Kbits, "kbit/s"),
        (SpeedUnit.Mbits, "Mbit/s")
    ];

    public TrayApplicationContext()
    {
        _settings = SettingsStore.Load();
        L.Apply(_settings.Language);
        _stats = new StatsStore();

        _menu = BuildContextMenu();
        MenuRenderer.Apply(_menu, ThemeHelper.GetAppPalette());

        _uiContext = SynchronizationContext.Current;

        // Das Panel ist die einzige Bedienoberflaeche - ShowTaskbarOverlay wird daher ignoriert.
        ReconcileOverlays();

        _monitor = new NetworkMonitor
        {
            AdapterId = _settings.AdapterId,
            IntervalMs = _settings.UpdateIntervalMs
        };
        _monitor.SampleReady += OnSample;
        _monitor.Start();

        _settings.AutostartEnabled = AutostartHelper.Synchronize();

        // Taskleisten-/Tray-Position ist normalerweise stabil, kann sich aber durch
        // Aufloesungswechsel, Monitorwechsel oder einen Explorer-Neustart aendern.
        _anchorRefreshTimer = new System.Windows.Forms.Timer { Interval = AnchorRefreshMs };
        _anchorRefreshTimer.Tick += (_, _) => ReconcileOverlays();
        _anchorRefreshTimer.Start();

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Application.ApplicationExit += OnApplicationExit;
    }

    /// <summary>
    /// Ein Panel je Taskleiste. Zuordnung ueber das Taskleisten-HWND: ein Explorer-Neustart
    /// oder Monitorwechsel erzeugt neue HWNDs, deren Panels dann neu angelegt werden;
    /// unveraenderte Taskleisten behalten ihr Panel und werden nur neu ausgerichtet.
    /// </summary>
    private void ReconcileOverlays()
    {
        var taskbars = TaskbarLayoutHelper.GetTaskbars(_settings.ShowOnAllMonitors);
        // Explorer startet gerade (neu) - bisherige Panels bis zum naechsten Durchlauf behalten.
        if (taskbars.Count == 0)
            return;

        var next = new List<TaskbarOverlayWindow>(taskbars.Count);
        var created = false;
        foreach (var taskbar in taskbars)
        {
            var overlay = _overlays.Find(o => o.TaskbarHandle == taskbar.Handle);
            if (overlay is null)
            {
                overlay = CreateOverlay(taskbar.Handle);
                created = true;
            }
            overlay.Reposition(taskbar.Bounds, taskbar.AnchorRight);
            if (!overlay.Visible)
                overlay.Show();
            next.Add(overlay);
        }

        foreach (var stale in _overlays.Except(next))
        {
            if (_flyoutOwner == stale)
                _flyoutOwner = null;
            stale.Dispose();
        }

        _overlays = next;
        if (created)
            PushValues();
    }

    private TaskbarOverlayWindow CreateOverlay(IntPtr taskbarHandle)
    {
        var overlay = new TaskbarOverlayWindow(_menu, taskbarHandle);
        // Das Overlay meldet PanelClicked bereits erst nach Ablauf der Doppelklickzeit.
        overlay.PanelClicked += (sender, _) =>
        {
            if (!_menu.Visible)
                ToggleFlyout((TaskbarOverlayWindow)sender!);
        };
        overlay.PanelDoubleClicked += (_, _) => OpenSettings();
        return overlay;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
            _uiContext.Post(_ => ReconcileOverlays(), null);
        else
            ReconcileOverlays();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        _headerItem = new SpeedHeaderItem();
        _headerItem.SetValues(SpeedFormatter.FormatFull(0, _settings.Unit), SpeedFormatter.FormatFull(0, _settings.Unit));
        menu.Items.Add(_headerItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.MenuShowStats, null, (_, _) => ShowFlyout(menu.SourceControl as TaskbarOverlayWindow));

        _unitMenu = new ToolStripMenuItem(L.MenuUnit);
        foreach (var (unit, text) in UnitChoices)
            _unitMenu.DropDownItems.Add(new RadioMenuItem(text, (_, _) => SetUnit(unit)) { Tag = unit });
        menu.Items.Add(_unitMenu);

        _adapterMenu = new ToolStripMenuItem(L.MenuAdapter);
        // Platzhalter, damit der Untermenue-Pfeil erscheint; echte Eintraege beim Oeffnen.
        _adapterMenu.DropDownItems.Add("…");
        menu.Items.Add(_adapterMenu);

        _autostartItem = new ToolStripMenuItem(L.MenuAutostart, null, (_, _) => ToggleAutostart());
        menu.Items.Add(_autostartItem);

        _allMonitorsItem = new ToolStripMenuItem(L.MenuAllMonitors, null, (_, _) => ToggleAllMonitors());
        menu.Items.Add(_allMonitorsItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.MenuSettings, null, (_, _) => OpenSettings());
        menu.Items.Add(L.MenuResetStats, null, (_, _) => ConfirmResetStats());
        menu.Items.Add(L.MenuAbout, null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.MenuExit, null, (_, _) => ExitApplication());

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
        _allMonitorsItem.Checked = _settings.ShowOnAllMonitors;
    }

    private void UpdateHeader() => _headerItem.SetValues(
        SpeedFormatter.FormatFull(_lastSample.UploadBytesPerSecond, _settings.Unit),
        SpeedFormatter.FormatFull(_lastSample.DownloadBytesPerSecond, _settings.Unit));

    private void RebuildAdapterMenu()
    {
        var items = _adapterMenu.DropDownItems;
        items.Clear();
        items.Add(new RadioMenuItem(L.AdapterAutoShort, (_, _) => SetAdapter("Auto"))
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
            items.Add(new RadioMenuItem(active ? ni.Name : $"{ni.Name} ({L.Inactive})", (_, _) => SetAdapter(id))
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

    private void ToggleAllMonitors()
    {
        _settings.ShowOnAllMonitors = !_settings.ShowOnAllMonitors;
        SettingsStore.Save(_settings);
        ReconcileOverlays();
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
        var tooltip = hideTooltip ? string.Empty : BuildTooltip();
        foreach (var overlay in _overlays)
            overlay.UpdateValues(up, down, _settings.Unit, tooltip);
        if (flyoutOpen)
            _flyout!.UpdateValues(_settings.Unit, up, down);
    }

    private string BuildTooltip()
    {
        var up = SpeedFormatter.FormatFull(_lastSample.UploadBytesPerSecond, _settings.Unit);
        var down = SpeedFormatter.FormatFull(_lastSample.DownloadBytesPerSecond, _settings.Unit);
        var today = _stats.Today;
        return $"↑ {up}   ↓ {down}\n"
               + $"{L.TooltipToday} ↑ {ByteFormatter.FormatBytes(today.UploadBytes)}   ↓ {ByteFormatter.FormatBytes(today.DownloadBytes)}";
    }

    private void ToggleFlyout(TaskbarOverlayWindow source)
    {
        // Ein Klick aufs Panel bei offenem Flyout kann es bereits per Fokusverlust geschlossen
        // haben - dann soll derselbe Klick es nicht sofort wieder oeffnen. Gilt nur fuer das
        // Panel, an dem es hing: ein Klick auf ein anderes Panel holt es dorthin.
        if (_flyout is { Visible: false } && source == _flyoutOwner
            && (DateTime.UtcNow - _flyout.LastDeactivatedUtc).TotalMilliseconds < SystemInformation.DoubleClickTime + 300)
            return;

        if (_flyout is { Visible: true } && source == _flyoutOwner)
            _flyout.Dismiss();
        else
            ShowFlyout(source);
    }

    private void ShowFlyout(TaskbarOverlayWindow? source)
    {
        var anchor = source is { IsDisposed: false } ? source : _overlays.FirstOrDefault();
        if (anchor is null)
            return;

        _flyout ??= new StatsFlyout(_stats, _monitor, _history);
        _flyoutOwner = anchor;
        _flyout.ShowNear(anchor.Bounds, _settings.Unit, _lastSample.UploadBytesPerSecond, _lastSample.DownloadBytesPerSecond);
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
        var languageChanged = result.Language != _settings.Language;

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
        if (languageChanged)
            ApplyLanguage();
        ReconcileOverlays();
        PushValues();
    }

    /// <summary>
    /// Sprachwechsel zur Laufzeit: Menue neu aufbauen, offene Fenster mit Texten schliessen
    /// bzw. neu oeffnen. Das Flyout zeichnet beim naechsten Oeffnen ohnehin neu.
    /// </summary>
    private void ApplyLanguage()
    {
        L.Apply(_settings.Language);

        var oldMenu = _menu;
        _menu = BuildContextMenu();
        MenuRenderer.Apply(_menu, ThemeHelper.GetAppPalette());
        foreach (var overlay in _overlays)
            overlay.ContextMenuStrip = _menu;
        oldMenu.Dispose();

        _flyout?.Dismiss();
        _aboutDialog?.Close();
        HistoryWindow.ReopenIfOpen();
    }

    private void ConfirmResetStats()
    {
        var confirmed = ConfirmDialog.Ask(
            "Speedy Monitor",
            L.ResetHeading,
            L.ResetMessage,
            L.ResetConfirm);
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
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Application.ApplicationExit -= OnApplicationExit;
        _stats.Save();
        _anchorRefreshTimer.Dispose();
        _monitor.Dispose();
        _settingsForm?.Close();
        _aboutDialog?.Close();
        _flyout?.Dispose();
        foreach (var overlay in _overlays)
            overlay.Dispose();
        _overlays.Clear();
        _menu.Dispose();
        Application.Exit();
    }
}
