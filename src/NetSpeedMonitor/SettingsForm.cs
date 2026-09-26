using System.Net.NetworkInformation;

namespace NetSpeedMonitor;

public sealed class SettingsForm : Form
{
    private readonly ComboBox _adapterCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _unitCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _languageCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly NumericUpDown _intervalUpDown = new() { Minimum = 250, Maximum = 5000, Increment = 250, Width = 90, TextAlign = HorizontalAlignment.Right };
    private readonly CheckBox _allMonitorsCheck = new() { Text = L.MenuAllMonitors, AutoSize = true };
    private readonly CheckBox _autostartCheck = new() { Text = L.MenuAutostart, AutoSize = true };
    private readonly List<string> _adapterIds = new();
    private readonly ThemePalette _palette = ThemeHelper.GetAppPalette();

    public AppSettings Result { get; }

    public SettingsForm(AppSettings current)
    {
        Result = current.Clone();

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        Text = L.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20, 16, 20, 16);
        ThemeHelper.ApplyAppIcon(this);

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var measureHeader = SectionHeader(L.GroupMeasurement, topMargin: 0);
        grid.Controls.Add(measureHeader);
        grid.SetColumnSpan(measureHeader, 2);
        AddRow(grid, L.MenuAdapter, _adapterCombo);
        AddRow(grid, L.MenuUnit, _unitCombo);

        var intervalPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        _intervalUpDown.Margin = Padding.Empty;
        intervalPanel.Controls.Add(_intervalUpDown);
        intervalPanel.Controls.Add(new Label { Text = "ms", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 0, 0, 0) });
        AddRow(grid, L.UpdateInterval, intervalPanel);

        var displayHeader = SectionHeader(L.GroupDisplay, topMargin: 18);
        grid.Controls.Add(displayHeader);
        grid.SetColumnSpan(displayHeader, 2);
        AddRow(grid, L.LanguageLabel, _languageCombo);
        _allMonitorsCheck.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(_allMonitorsCheck);
        grid.SetColumnSpan(_allMonitorsCheck, 2);

        var systemHeader = SectionHeader(L.GroupSystem, topMargin: 18);
        grid.Controls.Add(systemHeader);
        grid.SetColumnSpan(systemHeader, 2);
        _autostartCheck.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(_autostartCheck);
        grid.SetColumnSpan(_autostartCheck, 2);

        var buttons = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 22, 0, 0)
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var defaultsButton = DialogButton(L.Defaults);
        var okButton = DialogButton("OK");
        var cancelButton = DialogButton(L.Cancel);
        okButton.Margin = new Padding(0, 0, 8, 0);
        defaultsButton.Click += (_, _) => LoadValues(new AppSettings());
        okButton.Click += (_, _) =>
        {
            ApplyToResult();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        buttons.Controls.Add(defaultsButton, 0, 0);
        buttons.Controls.Add(okButton, 2, 0);
        buttons.Controls.Add(cancelButton, 3, 0);
        grid.Controls.Add(buttons);
        grid.SetColumnSpan(buttons, 2);

        Controls.Add(grid);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        PopulateUnits();
        _languageCombo.Items.AddRange(new object[] { L.LanguageAuto, L.LanguageGerman, L.LanguageEnglish });
        PopulateAdapters();
        LoadValues(current);
        SizeAdapterCombo();

        ThemeHelper.ApplyControlTheme(this, _palette);
        ThemeHelper.StylePrimaryButton(okButton, _palette);
        measureHeader.ForeColor = _palette.Text;
        displayHeader.ForeColor = _palette.Text;
        systemHeader.ForeColor = _palette.Text;
        ResumeLayout(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ThemeHelper.ApplyTitleBarTheme(this, _palette.IsDark);
    }

    private static Label SectionHeader(string text, int topMargin) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10.5f),
        Margin = new Padding(0, topMargin, 0, 8)
    };

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 24, 4)
        });
        control.Anchor = AnchorStyles.Left;
        if (control is ComboBox)
            control.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(control);
    }

    private static Button DialogButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MinimumSize = new Size(92, 30),
        Padding = new Padding(8, 0, 8, 0),
        Margin = Padding.Empty
    };

    private void PopulateAdapters()
    {
        _adapterCombo.Items.Add(L.AdapterAutoLong);
        _adapterIds.Add("Auto");

        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .OrderByDescending(ni => ni.OperationalStatus == OperationalStatus.Up)
            .ThenBy(ni => ni.Name);

        foreach (var ni in interfaces)
        {
            var status = ni.OperationalStatus == OperationalStatus.Up ? L.Active : L.Inactive;
            _adapterCombo.Items.Add($"{ni.Name} ({ni.Description}) – {status}");
            _adapterIds.Add(ni.Id);
        }
    }

    /// <summary>
    /// Breite am laengsten Eintrag ausrichten. Gemessen wird in Geraetepixeln, die Groesse
    /// wird aber vor der DPI-Skalierung gesetzt - daher zurueck auf 96 dpi umrechnen.
    /// </summary>
    private void SizeAdapterCombo()
    {
        var scale = DeviceDpi / 96f;
        var longest = _adapterCombo.Items.Cast<object>()
            .Select(o => TextRenderer.MeasureText(o.ToString(), Font).Width)
            .DefaultIfEmpty(0)
            .Max();
        var logicalLongest = (int)Math.Ceiling(longest / scale) + 32;
        _adapterCombo.Width = Math.Clamp(logicalLongest, 300, 420);
        _adapterCombo.DropDownWidth = Math.Max(_adapterCombo.Width, logicalLongest);
    }

    private void PopulateUnits()
    {
        _unitCombo.Items.AddRange(new object[]
        {
            L.UnitAuto,
            "KB/s",
            "MB/s",
            "kbit/s",
            "Mbit/s"
        });
    }

    private void LoadValues(AppSettings values)
    {
        var index = _adapterIds.IndexOf(values.AdapterId);
        _adapterCombo.SelectedIndex = index >= 0 ? index : 0;
        _unitCombo.SelectedIndex = values.Unit switch
        {
            SpeedUnit.KBs => 1,
            SpeedUnit.MBs => 2,
            SpeedUnit.Kbits => 3,
            SpeedUnit.Mbits => 4,
            _ => 0
        };
        _intervalUpDown.Value = Math.Clamp(values.UpdateIntervalMs, 250, 5000);
        // Reihenfolge der Eintraege entspricht den Enum-Werten Auto/German/English.
        _languageCombo.SelectedIndex = Enum.IsDefined(values.Language) ? (int)values.Language : 0;
        _allMonitorsCheck.Checked = values.ShowOnAllMonitors;
        _autostartCheck.Checked = values.AutostartEnabled;
    }

    private void ApplyToResult()
    {
        Result.AdapterId = _adapterIds[_adapterCombo.SelectedIndex];
        Result.Unit = _unitCombo.SelectedIndex switch
        {
            1 => SpeedUnit.KBs,
            2 => SpeedUnit.MBs,
            3 => SpeedUnit.Kbits,
            4 => SpeedUnit.Mbits,
            _ => SpeedUnit.Auto
        };
        Result.UpdateIntervalMs = (int)_intervalUpDown.Value;
        Result.Language = (AppLanguage)_languageCombo.SelectedIndex;
        Result.ShowOnAllMonitors = _allMonitorsCheck.Checked;
        Result.AutostartEnabled = _autostartCheck.Checked;
        // Das Panel ist die einzige Bedienoberflaeche und darf daher nie abgeschaltet sein.
        Result.ShowTaskbarOverlay = true;
    }
}
