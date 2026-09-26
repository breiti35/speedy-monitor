using System.Drawing.Drawing2D;

namespace NetSpeedMonitor;

/// <summary>
/// Rahmenloses Statistik-Popup direkt ueber dem Taskleisten-Panel (wie der Hover-Tooltip
/// des klassischen NetSpeedMonitor): aktuelle Werte, Summen und Verlaufsgraph.
/// Wird wiederverwendet (nur versteckt, nicht zerstoert) und schliesst bei Fokusverlust/Esc.
/// </summary>
public sealed class StatsFlyout : Form
{
    public const int HistoryLength = 60;

    private static readonly Color GraphUpload = Color.FromArgb(255, 152, 0);
    private static readonly Color GraphDownload = Color.FromArgb(0, 205, 120);

    private const float LogicalWidth = 340;

    private readonly StatsStore _stats;
    private readonly NetworkMonitor _monitor;
    private readonly IReadOnlyCollection<(double Up, double Down)> _history;

    private readonly Font _titleFont = new("Segoe UI Semibold", 10.5f);
    private readonly Font _speedFont = new("Segoe UI Semibold", 15f);
    private readonly Font _textFont = new("Segoe UI", 9f);
    private readonly Font _smallFont = new("Segoe UI", 8f);

    private ThemePalette _p = ThemeHelper.GetAppPalette();
    private SpeedUnit _unit;
    private double _up;
    private double _down;
    private bool _dismissing;

    /// <summary>Zeitpunkt, zu dem das Flyout zuletzt durch Fokusverlust geschlossen wurde.</summary>
    public DateTime LastDeactivatedUtc { get; private set; }

    public StatsFlyout(StatsStore stats, NetworkMonitor monitor, IReadOnlyCollection<(double Up, double Down)> history)
    {
        _stats = stats;
        _monitor = monitor;
        _history = history;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Text = "Speedy Monitor – Statistik";
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = _p.Surface;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ThemeHelper.ApplyRoundedCorners(Handle, ThemeHelper.DWMWCP_ROUND, _p.Border);
    }

    public void ApplyPalette(ThemePalette palette)
    {
        _p = palette;
        BackColor = palette.Surface;
        if (IsHandleCreated)
            ThemeHelper.ApplyRoundedCorners(Handle, ThemeHelper.DWMWCP_ROUND, palette.Border);
        Invalidate();
    }

    /// <summary>
    /// Zeigt das Flyout rechtsbuendig am Panel an: unterhalb, wenn das Panel in der oberen
    /// Bildschirmhaelfte liegt (Taskleiste oben), sonst oberhalb.
    /// </summary>
    public void ShowNear(Rectangle panelBounds, SpeedUnit unit, double up, double down)
    {
        _unit = unit;
        _up = up;
        _down = down;

        var scale = DeviceDpi / 96f;
        Size = new Size((int)Math.Round(LogicalWidth * scale), MeasureHeight(scale));

        var gap = (int)Math.Round(8 * scale);
        var screen = Screen.FromRectangle(panelBounds);
        var work = screen.WorkingArea;
        var panelCenterY = panelBounds.Top + panelBounds.Height / 2;
        var below = panelCenterY < screen.Bounds.Top + screen.Bounds.Height / 2;
        var x = panelBounds.Right - Width;
        var y = below ? panelBounds.Bottom + gap : panelBounds.Top - gap - Height;
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - Width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - Height));
        Location = new Point(x, y);

        Show();
        WindowActivation.ForceForeground(this);
        Invalidate();
    }

    public void UpdateValues(SpeedUnit unit, double up, double down)
    {
        _unit = unit;
        _up = up;
        _down = down;
        if (Visible)
            Invalidate();
    }

    /// <summary>Schliesst das Flyout von aussen, ohne als "Fokusverlust" zu zaehlen.</summary>
    public void Dismiss()
    {
        _dismissing = true;
        Hide();
        _dismissing = false;
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (_dismissing || !Visible)
            return;
        LastDeactivatedUtc = DateTime.UtcNow;
        Hide();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Dismiss();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private int MeasureHeight(float scale)
    {
        var layout = ComputeLayout(scale);
        return layout.Bottom;
    }

    private readonly record struct FlyoutLayout(
        int Pad, int TitleY, int CaptionY, int SpeedY, int TableY, int RowHeight,
        int GraphY, int GraphHeight, int FooterLineY, int FooterY, int Bottom);

    private FlyoutLayout ComputeLayout(float scale)
    {
        int S(float v) => (int)Math.Round(v * scale);

        var pad = S(16);
        var titleY = pad;
        var captionY = titleY + _titleFont.Height + S(10);
        var speedY = captionY + _smallFont.Height;
        var tableY = speedY + _speedFont.Height + S(14);
        var rowHeight = _textFont.Height + S(6);
        var graphY = tableY + rowHeight * 5 + S(12);
        var graphHeight = S(72);
        var footerLineY = graphY + graphHeight + S(12);
        var footerY = footerLineY + S(9);
        var bottom = footerY + _smallFont.Height + S(12);
        return new FlyoutLayout(pad, titleY, captionY, speedY, tableY, rowHeight, graphY, graphHeight, footerLineY, footerY, bottom);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var scale = DeviceDpi / 96f;
        int S(float v) => (int)Math.Round(v * scale);
        var l = ComputeLayout(scale);
        var contentWidth = ClientSize.Width - l.Pad * 2;
        var half = contentWidth / 2;

        g.Clear(_p.Surface);
        if (!ThemeHelper.SupportsRoundedCorners)
        {
            using var borderPen = new Pen(_p.Border);
            g.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        const TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        const TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        TextRenderer.DrawText(g, "Speedy Monitor", _titleFont, new Rectangle(l.Pad, l.TitleY, contentWidth, _titleFont.Height), _p.Text, left);

        // Aktuelle Geschwindigkeit, gross
        TextRenderer.DrawText(g, "Upload", _smallFont, new Rectangle(l.Pad, l.CaptionY, half, _smallFont.Height), _p.SubtleText, left);
        TextRenderer.DrawText(g, "Download", _smallFont, new Rectangle(l.Pad + half, l.CaptionY, half, _smallFont.Height), _p.SubtleText, left);
        TextRenderer.DrawText(g, "↑ " + SpeedFormatter.FormatFull(_up, _unit), _speedFont,
            new Rectangle(l.Pad, l.SpeedY, half, _speedFont.Height), _p.Upload, left);
        TextRenderer.DrawText(g, "↓ " + SpeedFormatter.FormatFull(_down, _unit), _speedFont,
            new Rectangle(l.Pad + half, l.SpeedY, half, _speedFont.Height), _p.Download, left);

        // Tabelle
        var colLabel = (int)(contentWidth * 0.36);
        var colWidth = (contentWidth - colLabel) / 2;
        var y = l.TableY;
        TextRenderer.DrawText(g, "Upload", _smallFont, new Rectangle(l.Pad + colLabel, y, colWidth, l.RowHeight), _p.SubtleText, right | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, "Download", _smallFont, new Rectangle(l.Pad + colLabel + colWidth, y, colWidth, l.RowHeight), _p.SubtleText, right | TextFormatFlags.VerticalCenter);
        using (var linePen = new Pen(_p.Border))
            g.DrawLine(linePen, l.Pad, y + l.RowHeight - 1, l.Pad + contentWidth, y + l.RowHeight - 1);

        var session = _stats.SessionTotal;
        var today = _stats.Today;
        var week = _stats.WeekTotal();
        var month = _stats.MonthTotal();
        var rows = new (string Label, long Up, long Down)[]
        {
            ("Sitzung", session.Upload, session.Download),
            ("Heute", today.UploadBytes, today.DownloadBytes),
            ("Woche (7 Tage)", week.Upload, week.Download),
            ("Monat", month.Upload, month.Download)
        };
        foreach (var row in rows)
        {
            y += l.RowHeight;
            var vc = TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, row.Label, _textFont, new Rectangle(l.Pad, y, colLabel, l.RowHeight), _p.SubtleText, left | vc);
            TextRenderer.DrawText(g, ByteFormatter.FormatBytes(row.Up), _textFont, new Rectangle(l.Pad + colLabel, y, colWidth, l.RowHeight), _p.Text, right | vc);
            TextRenderer.DrawText(g, ByteFormatter.FormatBytes(row.Down), _textFont, new Rectangle(l.Pad + colLabel + colWidth, y, colWidth, l.RowHeight), _p.Text, right | vc);
        }

        DrawGraph(g, new Rectangle(l.Pad, l.GraphY, contentWidth, l.GraphHeight), scale);

        using (var linePen = new Pen(_p.Border))
            g.DrawLine(linePen, 0, l.FooterLineY, ClientSize.Width, l.FooterLineY);

        var names = _monitor.MonitoredAdapterNames;
        var adapterText = names.Count == 0 ? "Kein aktiver Adapter" : string.Join(", ", names);
        var durationText = "seit " + FormatDuration(DateTime.Now - _stats.SessionStart);
        var durationWidth = TextRenderer.MeasureText(g, durationText, _smallFont, Size.Empty, TextFormatFlags.NoPadding).Width;
        TextRenderer.DrawText(g, adapterText, _smallFont,
            new Rectangle(l.Pad, l.FooterY, contentWidth - durationWidth - S(12), _smallFont.Height), _p.SubtleText, left);
        TextRenderer.DrawText(g, durationText, _smallFont,
            new Rectangle(l.Pad, l.FooterY, contentWidth, _smallFont.Height), _p.SubtleText, right);
    }

    private void DrawGraph(Graphics g, Rectangle rect, float scale)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var frame = ThemeHelper.RoundedRect(rect, 6 * scale);
        using (var bg = new SolidBrush(_p.Background))
            g.FillPath(bg, frame);

        var samples = _history.ToArray();
        // Mindestens 1 KB/s als Skala, damit Leerlauf-Rauschen nicht bildfuellend wirkt.
        var max = 1024.0;
        foreach (var s in samples)
            max = Math.Max(max, Math.Max(s.Up, s.Down));

        var plot = RectangleF.Inflate(rect, -1, -1);
        var top = plot.Top + plot.Height * 0.22f;
        var usable = plot.Bottom - top;

        using (var gridPen = new Pen(Color.FromArgb(60, _p.SubtleText)) { DashStyle = DashStyle.Dash })
            g.DrawLine(gridPen, plot.Left + 4, top, plot.Right - 4, top);

        var state = g.Save();
        g.SetClip(frame);
        if (samples.Length >= 2)
        {
            var step = plot.Width / (HistoryLength - 1);
            PointF[] Points(Func<(double Up, double Down), double> sel)
            {
                var pts = new PointF[samples.Length];
                for (var i = 0; i < samples.Length; i++)
                {
                    var x = plot.Right - (samples.Length - 1 - i) * step;
                    var v = (float)(sel(samples[i]) / max);
                    pts[i] = new PointF(x, plot.Bottom - v * usable);
                }
                return pts;
            }

            DrawSeries(g, Points(s => s.Down), GraphDownload, plot.Bottom, scale);
            DrawSeries(g, Points(s => s.Up), GraphUpload, plot.Bottom, scale);
        }
        g.Restore(state);

        TextRenderer.DrawText(g, SpeedFormatter.FormatFull(max, _unit), _smallFont,
            new Point(rect.Left + (int)(6 * scale), rect.Top + (int)(3 * scale)), _p.SubtleText, TextFormatFlags.NoPadding);
    }

    private static void DrawSeries(Graphics g, PointF[] pts, Color color, float bottom, float scale)
    {
        using var area = new GraphicsPath();
        area.AddLine(pts[0].X, bottom, pts[0].X, pts[0].Y);
        area.AddLines(pts);
        area.AddLine(pts[^1].X, pts[^1].Y, pts[^1].X, bottom);
        area.CloseFigure();
        using (var fill = new SolidBrush(Color.FromArgb(50, color)))
            g.FillPath(fill, area);
        using var pen = new Pen(color, 1.5f * scale) { LineJoin = LineJoin.Round };
        g.DrawLines(pen, pts);
    }

    private static string FormatDuration(TimeSpan span)
    {
        var hours = (int)span.TotalHours;
        if (hours > 0)
            return $"{hours} h {span.Minutes} min";
        return span.TotalMinutes < 1 ? "< 1 min" : $"{span.Minutes} min";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _speedFont.Dispose();
            _textFont.Dispose();
            _smallFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
