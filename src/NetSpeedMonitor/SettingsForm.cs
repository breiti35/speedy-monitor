using System.Net.NetworkInformation;

namespace NetSpeedMonitor;

public sealed class SettingsForm : Form
{
    private readonly ComboBox _adapterCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly ComboBox _unitCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly NumericUpDown _intervalUpDown = new() { Minimum = 250, Maximum = 5000, Increment = 250, Width = 300 };
    private readonly CheckBox _autostartCheck = new() { Text = "Mit Windows starten", AutoSize = true };
    private readonly CheckBox _overlayCheck = new() { Text = "Textanzeige in der Taskleiste (neben der Uhr)", AutoSize = true };
    private readonly List<string> _adapterIds = new();

    public AppSettings Result { get; }

    public SettingsForm(AppSettings current)
    {
        Result = current.Clone();

        Text = "NetSpeed Monitor – Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(346, 290);
        Font = SystemFonts.MessageBoxFont;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(14, 14, 14, 0)
        };
        layout.Controls.Add(Label("Netzwerkadapter:"));
        layout.Controls.Add(_adapterCombo);
        layout.Controls.Add(Label("Einheit:", topMargin: 12));
        layout.Controls.Add(_unitCombo);
        layout.Controls.Add(Label("Aktualisierungsintervall (ms):", topMargin: 12));
        layout.Controls.Add(_intervalUpDown);
        _overlayCheck.Margin = new Padding(0, 16, 0, 0);
        layout.Controls.Add(_overlayCheck);
        _autostartCheck.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(_autostartCheck);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(14, 8, 14, 8)
        };
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 84 };
        var cancelButton = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 84, Margin = new Padding(0, 0, 8, 0) };
        okButton.Click += (_, _) => ApplyToResult();
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        Controls.Add(layout);
        Controls.Add(buttonPanel);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        PopulateAdapters(current.AdapterId);
        PopulateUnits(current.Unit);
        _intervalUpDown.Value = Math.Clamp(current.UpdateIntervalMs, 250, 5000);
        _autostartCheck.Checked = current.AutostartEnabled;
        _overlayCheck.Checked = current.ShowTaskbarOverlay;
    }

    private static Label Label(string text, int topMargin = 0) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, topMargin, 0, 4)
    };

    private void PopulateAdapters(string currentId)
    {
        _adapterCombo.Items.Clear();
        _adapterIds.Clear();

        _adapterCombo.Items.Add("Automatisch (alle aktiven Adapter)");
        _adapterIds.Add("Auto");

        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderByDescending(ni => ni.OperationalStatus == OperationalStatus.Up)
            .ThenBy(ni => ni.Name);

        foreach (var ni in interfaces)
        {
            var status = ni.OperationalStatus == OperationalStatus.Up ? "aktiv" : "inaktiv";
            _adapterCombo.Items.Add($"{ni.Name} ({ni.Description}) – {status}");
            _adapterIds.Add(ni.Id);
        }

        var index = _adapterIds.IndexOf(currentId);
        _adapterCombo.SelectedIndex = index >= 0 ? index : 0;
    }

    private void PopulateUnits(SpeedUnit current)
    {
        _unitCombo.Items.AddRange(new object[]
        {
            "Automatisch (B/s, KB/s, MB/s)",
            "KB/s",
            "MB/s",
            "kbit/s",
            "Mbit/s"
        });

        _unitCombo.SelectedIndex = current switch
        {
            SpeedUnit.KBs => 1,
            SpeedUnit.MBs => 2,
            SpeedUnit.Kbits => 3,
            SpeedUnit.Mbits => 4,
            _ => 0
        };
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
        Result.AutostartEnabled = _autostartCheck.Checked;
        Result.ShowTaskbarOverlay = _overlayCheck.Checked;
    }
}
