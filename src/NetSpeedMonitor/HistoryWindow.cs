using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace NetSpeedMonitor;

/// <summary>
/// Traffic-Verlauf pro Tag bzw. Monat als Balkendiagramm, mit Summen und CSV-Export.
/// Es gibt hoechstens ein Fenster; ein erneuter Aufruf holt es nur nach vorne.
/// </summary>
public sealed class HistoryWindow : Form
{
    private enum HistoryRange { Days30, Days90, Months12 }

    private static HistoryWindow? _instance;

    private readonly StatsStore _stats;
    private readonly HistoryChart _chart = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
    private readonly Label _heading = new() { Text = "Datenverbrauch", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI Semibold", 11f), Margin = Padding.Empty };
    private readonly Button[] _rangeButtons;
    private readonly Label[] _summaryCaptions;
    private readonly Label _totalUp = SummaryValue();
    private readonly Label _totalDown = SummaryValue();
    private readonly Label _avgUp = SummaryValue();
    private readonly Label _avgDown = SummaryValue();
    private readonly Label _status = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(12, 0, 12, 0) };
    private readonly Button _closeButton;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 30_000 };

    private ThemePalette _palette = ThemeHelper.GetAppPalette();
    private HistoryRange _range = HistoryRange.Days30;
    private bool _statusIsError;

    public static void ShowOrActivate(StatsStore stats)
    {
        if (_instance is { IsDisposed: false })
        {
            if (_instance.WindowState == FormWindowState.Minimized)
                _instance.WindowState = FormWindowState.Normal;
            _instance.RefreshData();
            WindowActivation.ForceForeground(_instance);
            return;
        }

        _instance = new HistoryWindow(stats);
        _instance.Show();
        WindowActivation.ForceForeground(_instance);
    }

    private HistoryWindow(StatsStore stats)
    {
        _stats = stats;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        Text = "Speedy Monitor – Verlauf";
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 480);
        MinimumSize = new Size(520, 380);
        Padding = new Padding(16, 14, 16, 14);
        ThemeHelper.ApplyAppIcon(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Kopf: Ueberschrift links, Zeitraum-Auswahl rechts
        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 0, 0, 10) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(_heading, 0, 0);
        var rangePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        _rangeButtons = new[]
        {
            RangeButton("30 Tage", HistoryRange.Days30),
            RangeButton("90 Tage", HistoryRange.Days90),
            RangeButton("12 Monate", HistoryRange.Months12)
        };
        _rangeButtons[0].Margin = Padding.Empty;
        rangePanel.Controls.AddRange(_rangeButtons);
        header.Controls.Add(rangePanel, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(_chart, 0, 1);

        var summary = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Margin = new Padding(0, 0, 0, 14) };
        for (var i = 0; i < 4; i++)
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _summaryCaptions = new[] { "Upload gesamt", "Download gesamt", "Ø Upload pro Tag", "Ø Download pro Tag" }
            .Select(t => new Label { Text = t, AutoSize = true, Margin = new Padding(0, 0, 8, 2) })
            .ToArray();
        var values = new[] { _totalUp, _totalDown, _avgUp, _avgDown };
        for (var i = 0; i < 4; i++)
        {
            summary.Controls.Add(_summaryCaptions[i], i, 0);
            summary.Controls.Add(values[i], i, 1);
        }
        root.Controls.Add(summary, 0, 2);

        var buttons = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var exportButton = new Button { Text = "Als CSV exportieren…", AutoSize = true, MinimumSize = new Size(92, 30), Padding = new Padding(8, 0, 8, 0), Margin = Padding.Empty };
        _closeButton = new Button { Text = "Schließen", AutoSize = true, MinimumSize = new Size(92, 30), Margin = Padding.Empty };
        exportButton.Click += (_, _) => ExportCsv();
        _closeButton.Click += (_, _) => Close();
        buttons.Controls.Add(exportButton, 0, 0);
        buttons.Controls.Add(_status, 1, 0);
        buttons.Controls.Add(_closeButton, 2, 0);
        root.Controls.Add(buttons, 0, 3);

        Controls.Add(root);
        CancelButton = _closeButton;
        // Sonst bekaeme die erste Zeitraum-Schaltflaeche den Fokusrahmen, obwohl sie nicht gewaehlt ist.
        ActiveControl = _closeButton;

        ApplyPalette(_palette);
        ResumeLayout(true);

        _refreshTimer.Tick += (_, _) => RefreshData();
        _refreshTimer.Start();
        RefreshData();
    }

    private static Label SummaryValue() =>
        new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11f), Margin = new Padding(0, 0, 8, 0) };

    private Button RangeButton(string text, HistoryRange range)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(84, 28), Margin = new Padding(6, 0, 0, 0), Tag = range };
        button.Click += (_, _) =>
        {
            _range = range;
            StyleRangeButtons();
            RefreshData();
        };
        return button;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ThemeHelper.ApplyTitleBarTheme(this, _palette.IsDark);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        if (_instance == this)
            _instance = null;
        base.OnFormClosed(e);
    }

    private void ApplyPalette(ThemePalette palette)
    {
        _palette = palette;
        ThemeHelper.ApplyControlTheme(this, palette);
        ThemeHelper.StylePrimaryButton(_closeButton, palette);
        StyleRangeButtons();
        foreach (var caption in _summaryCaptions)
            caption.ForeColor = palette.SubtleText;
        _totalUp.ForeColor = _avgUp.ForeColor = palette.Upload;
        _totalDown.ForeColor = _avgDown.ForeColor = palette.Download;
        _status.ForeColor = _statusIsError ? palette.Upload : palette.SubtleText;
        _chart.SetPalette(palette);
        if (IsHandleCreated)
            ThemeHelper.ApplyTitleBarTheme(this, palette.IsDark);
    }

    private void StyleRangeButtons()
    {
        foreach (var b in _rangeButtons)
        {
            if ((HistoryRange)b.Tag! == _range)
            {
                ThemeHelper.StylePrimaryButton(b, _palette);
                continue;
            }
            b.BackColor = _palette.Surface;
            b.ForeColor = _palette.Text;
            b.FlatAppearance.BorderColor = _palette.Border;
            b.FlatAppearance.MouseOverBackColor = _palette.Hover;
            b.FlatAppearance.MouseDownBackColor = _palette.Border;
        }
    }

    private void RefreshData()
    {
        // Theme-Wechsel waehrend das Fenster offen ist mitnehmen - der Timer laeuft ohnehin.
        var palette = ThemeHelper.GetAppPalette();
        if (palette != _palette)
            ApplyPalette(palette);

        var monthly = _range == HistoryRange.Months12;
        var buckets = BuildBuckets(_stats.GetDailyTotals(), _range, DateTime.Now.Date);
        _chart.SetData(buckets, monthly);

        var up = buckets.Sum(b => b.Upload);
        var down = buckets.Sum(b => b.Download);
        // Durchschnitt nur ueber erfasste Tage: Tage ohne laufende App sind unbekannt, nicht 0.
        var days = buckets.Sum(b => b.DaysWithData);
        _totalUp.Text = ByteFormatter.FormatBytes(up);
        _totalDown.Text = ByteFormatter.FormatBytes(down);
        _avgUp.Text = days == 0 ? "–" : ByteFormatter.FormatBytes(up / days);
        _avgDown.Text = days == 0 ? "–" : ByteFormatter.FormatBytes(down / days);
    }

    private static List<HistoryBucket> BuildBuckets(IReadOnlyList<DayTotal> days, HistoryRange range, DateTime today)
    {
        var buckets = new List<HistoryBucket>();
        if (range == HistoryRange.Months12)
        {
            var first = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
            for (var i = 0; i < 12; i++)
            {
                var start = first.AddMonths(i);
                var end = start.AddMonths(1);
                var inMonth = days.Where(d => d.Date >= start && d.Date < end).ToList();
                buckets.Add(new HistoryBucket(start, inMonth.Sum(d => d.UploadBytes), inMonth.Sum(d => d.DownloadBytes), inMonth.Count));
            }
            return buckets;
        }

        var count = range == HistoryRange.Days90 ? 90 : 30;
        var byDate = days.ToDictionary(d => d.Date.Date);
        for (var i = count - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            buckets.Add(byDate.TryGetValue(date, out var d)
                ? new HistoryBucket(date, d.UploadBytes, d.DownloadBytes, 1)
                : new HistoryBucket(date, 0, 0, 0));
        }
        return buckets;
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Verlauf als CSV exportieren",
            Filter = "CSV-Datei (*.csv)|*.csv|Alle Dateien (*.*)|*.*",
            DefaultExt = "csv",
            AddExtension = true,
            FileName = $"SpeedyMonitor_Verlauf_{DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            // BOM, damit Excel die Datei als UTF-8 erkennt (sonst kaputte Umlaute/Einheiten).
            File.WriteAllText(dialog.FileName, BuildCsv(_stats.GetDailyTotals()), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            SetStatus($"Exportiert: {Path.GetFileName(dialog.FileName)}", isError: false);
        }
        catch (Exception ex)
        {
            AppLog.Error($"CSV-Export fehlgeschlagen: {dialog.FileName}", ex);
            SetStatus("Export fehlgeschlagen – ist die Datei noch in einem anderen Programm geöffnet?", isError: true);
        }
    }

    private static string BuildCsv(IReadOnlyList<DayTotal> days)
    {
        var sb = new StringBuilder();
        sb.Append("Datum;Upload (Bytes);Download (Bytes);Upload;Download\r\n");
        foreach (var d in days)
        {
            sb.Append(d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';')
              .Append(d.UploadBytes.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(d.DownloadBytes.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(ByteFormatter.FormatBytes(d.UploadBytes)).Append(';')
              .Append(ByteFormatter.FormatBytes(d.DownloadBytes)).Append("\r\n");
        }
        return sb.ToString();
    }

    private void SetStatus(string text, bool isError)
    {
        _statusIsError = isError;
        _status.Text = text;
        _status.ForeColor = isError ? _palette.Upload : _palette.SubtleText;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _refreshTimer.Dispose();
        base.Dispose(disposing);
    }
}
