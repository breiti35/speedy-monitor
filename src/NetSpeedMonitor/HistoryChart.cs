using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace NetSpeedMonitor;

/// <summary>Ein Balkenpaar im Verlauf: ein Tag oder ein Monat.</summary>
public readonly record struct HistoryBucket(DateTime Start, long Upload, long Download, int DaysWithData);

/// <summary>Owner-drawn Balkendiagramm (Upload/Download nebeneinander) mit Hover-Details.</summary>
public sealed class HistoryChart : Control
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static readonly int[] DayLabelSteps = { 1, 2, 3, 5, 7, 14, 15, 30 };
    private static readonly int[] MonthLabelSteps = { 1, 2, 3, 4, 6, 12 };

    private readonly Font _font = new("Segoe UI", 8.25f);
    private readonly Font _tipFont = new("Segoe UI", 9f);
    private readonly Font _tipTitleFont = new("Segoe UI Semibold", 9f);

    private IReadOnlyList<HistoryBucket> _buckets = Array.Empty<HistoryBucket>();
    private bool _monthly;
    private ThemePalette _p = ThemePalette.Dark;
    private int _hover = -1;
    private Point _mouse;

    // Aus dem letzten Paint, fuer die Hover-Trefferpruefung.
    private float _plotLeft;
    private float _slotWidth;

    public HistoryChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        DoubleBuffered = true;
    }

    public void SetPalette(ThemePalette palette)
    {
        _p = palette;
        Invalidate();
    }

    public void SetData(IReadOnlyList<HistoryBucket> buckets, bool monthly)
    {
        _buckets = buckets;
        _monthly = monthly;
        if (_hover >= buckets.Count)
            _hover = -1;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;
        var index = -1;
        if (_slotWidth > 0 && e.X >= _plotLeft)
        {
            var i = (int)((e.X - _plotLeft) / _slotWidth);
            if (i < _buckets.Count)
                index = i;
        }
        _hover = index;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var scale = DeviceDpi / 96f;
        int S(float v) => (int)Math.Round(v * scale);

        g.Clear(_p.Background);
        var card = new RectangleF(0, 0, Width - 1, Height - 1);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var cardPath = ThemeHelper.RoundedRect(card, 8 * scale))
        using (var cardBrush = new SolidBrush(_p.Surface))
        using (var cardPen = new Pen(_p.Border))
        {
            g.FillPath(cardBrush, cardPath);
            g.DrawPath(cardPen, cardPath);
        }
        g.SmoothingMode = SmoothingMode.None;

        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        var pad = S(14);
        var fontHeight = _font.Height;

        // Kopfzeile: Aufloesung links, Legende rechts
        TextRenderer.DrawText(g, _monthly ? "Pro Monat" : "Pro Tag", _font, new Point(pad, pad), _p.SubtleText, flags);
        var legendX = Width - pad;
        foreach (var (text, color) in new[] { ("Download", _p.Download), ("Upload", _p.Upload) })
        {
            var w = TextRenderer.MeasureText(g, text, _font, Size.Empty, flags).Width;
            legendX -= w;
            TextRenderer.DrawText(g, text, _font, new Point(legendX, pad), _p.Text, flags);
            var box = S(9);
            legendX -= box + S(6);
            using (var brush = new SolidBrush(color))
                g.FillRectangle(brush, legendX, pad + (fontHeight - box) / 2, box, box);
            legendX -= S(16);
        }

        var plotTop = pad + fontHeight + S(14);
        var plotBottom = Height - pad - fontHeight - S(8);
        var plotHeight = plotBottom - plotTop;

        var max = 0L;
        foreach (var b in _buckets)
            max = Math.Max(max, Math.Max(b.Upload, b.Download));
        var (step, count) = ComputeScale(max);
        var top = step * count;

        var yLabels = Enumerable.Range(0, count + 1).Select(i => ByteFormatter.FormatBytes(step * i)).ToArray();
        var yLabelWidth = yLabels.Max(t => TextRenderer.MeasureText(g, t, _font, Size.Empty, flags).Width);
        var plotLeft = pad + yLabelWidth + S(10);
        var plotRight = Width - pad;
        var plotWidth = plotRight - plotLeft;

        _plotLeft = plotLeft;
        _slotWidth = 0;
        if (plotHeight < S(24) || plotWidth < S(40) || _buckets.Count == 0)
            return;

        var slot = (float)plotWidth / _buckets.Count;
        _slotWidth = slot;

        if (_hover >= 0)
        {
            using var hoverBrush = new SolidBrush(_p.Hover);
            var hx = (int)Math.Round(plotLeft + _hover * slot);
            var hr = (int)Math.Round(plotLeft + (_hover + 1) * slot);
            g.FillRectangle(hoverBrush, hx, plotTop - S(4), Math.Max(1, hr - hx), plotBottom - plotTop + S(4));
        }

        using (var gridPen = new Pen(Color.FromArgb(_p.IsDark ? 40 : 60, _p.SubtleText)))
        using (var basePen = new Pen(_p.Border))
        {
            for (var i = 0; i <= count; i++)
            {
                var y = plotBottom - (int)Math.Round(plotHeight * (double)i / count);
                g.DrawLine(i == 0 ? basePen : gridPen, plotLeft, y, plotRight, y);
                TextRenderer.DrawText(g, yLabels[i], _font,
                    new Rectangle(pad, y - fontHeight / 2, yLabelWidth, fontHeight), _p.SubtleText,
                    flags | TextFormatFlags.Right);
            }
        }

        // Balkenpaar nimmt ~70 % des Slots ein, damit benachbarte Tage getrennt bleiben.
        var groupWidth = slot * 0.72f;
        var gap = slot >= S(10) ? Math.Max(1, S(1.5f)) : 0;
        var barWidth = Math.Max(1f, (groupWidth - gap) / 2);
        using (var upBrush = new SolidBrush(_p.Upload))
        using (var downBrush = new SolidBrush(_p.Download))
        {
            for (var i = 0; i < _buckets.Count; i++)
            {
                var b = _buckets[i];
                var x0 = plotLeft + i * slot + (slot - (barWidth * 2 + gap)) / 2;
                FillBar(g, upBrush, x0, x0 + barWidth, b.Upload, top, plotBottom, plotHeight);
                FillBar(g, downBrush, x0 + barWidth + gap, x0 + barWidth * 2 + gap, b.Download, top, plotBottom, plotHeight);
            }
        }

        DrawXLabels(g, plotLeft, slot, plotBottom + S(6), pad, flags);

        if (_hover >= 0)
            DrawTooltip(g, _buckets[_hover], scale);
    }

    private static void FillBar(Graphics g, Brush brush, float left, float right, long value, long top, int bottom, int height)
    {
        if (value <= 0)
            return;
        // Mindestens 1 px, damit auch kleine Werte neben grossen sichtbar bleiben.
        var h = Math.Max(1, (int)Math.Round(height * (double)value / top));
        var l = (int)Math.Round(left);
        var r = Math.Max(l + 1, (int)Math.Round(right));
        g.FillRectangle(brush, l, bottom - h, r - l, h);
    }

    private void DrawXLabels(Graphics g, int plotLeft, float slot, int y, int pad, TextFormatFlags flags)
    {
        var format = _monthly ? "MMM yy" : "dd.MM.";
        var sample = _monthly ? "Sept. 26" : "28.08.";
        var needed = TextRenderer.MeasureText(g, sample, _font, Size.Empty, flags).Width * 1.4f;
        var steps = _monthly ? MonthLabelSteps : DayLabelSteps;
        var k = steps.FirstOrDefault(s => s * slot >= needed);
        if (k == 0)
            k = steps[^1];

        // Vom juengsten Eintrag rueckwaerts, damit "heute"/der laufende Monat immer beschriftet ist.
        var lastLeft = int.MaxValue;
        for (var i = _buckets.Count - 1; i >= 0; i -= k)
        {
            var text = _buckets[i].Start.ToString(format, German);
            var w = TextRenderer.MeasureText(g, text, _font, Size.Empty, flags).Width;
            var center = plotLeft + (i + 0.5f) * slot;
            var x = (int)Math.Round(center - w / 2f);
            x = Math.Clamp(x, pad, Math.Max(pad, Width - pad - w));
            if (x + w > lastLeft - 4)
                continue;
            TextRenderer.DrawText(g, text, _font, new Point(x, y), _p.SubtleText, flags);
            lastLeft = x;
        }
    }

    private void DrawTooltip(Graphics g, HistoryBucket b, float scale)
    {
        int S(float v) => (int)Math.Round(v * scale);
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        var title = _monthly
            ? b.Start.ToString("MMMM yyyy", German)
            : b.Start.ToString("dddd, dd.MM.yyyy", German);

        var rows = new List<(string Label, string Value, Color Color)>();
        if (b.DaysWithData == 0)
        {
            rows.Add(("Keine Daten", "", _p.SubtleText));
        }
        else
        {
            rows.Add(("↑ Upload", ByteFormatter.FormatBytes(b.Upload), _p.Upload));
            rows.Add(("↓ Download", ByteFormatter.FormatBytes(b.Download), _p.Download));
            if (_monthly)
                rows.Add((b.DaysWithData == 1 ? "1 Tag erfasst" : $"{b.DaysWithData} Tage erfasst", "", _p.SubtleText));
        }

        var pad = S(10);
        var lineHeight = _tipFont.Height + S(3);
        var labelWidth = rows.Max(r => TextRenderer.MeasureText(g, r.Label, _tipFont, Size.Empty, flags).Width);
        var valueWidth = rows.Max(r => r.Value.Length == 0 ? 0 : TextRenderer.MeasureText(g, r.Value, _tipFont, Size.Empty, flags).Width);
        var titleWidth = TextRenderer.MeasureText(g, title, _tipTitleFont, Size.Empty, flags).Width;
        var width = Math.Max(titleWidth, labelWidth + S(16) + valueWidth) + pad * 2;
        var height = pad * 2 + _tipTitleFont.Height + S(6) + rows.Count * lineHeight - S(3);

        var x = _mouse.X + S(14);
        if (x + width > Width - S(4))
            x = _mouse.X - S(14) - width;
        x = Math.Max(S(4), x);
        var y = _mouse.Y - height - S(8);
        if (y < S(4))
            y = Math.Min(_mouse.Y + S(20), Height - height - S(4));

        var rect = new Rectangle(x, y, width, height);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = ThemeHelper.RoundedRect(rect, 6 * scale))
        using (var bg = new SolidBrush(_p.Background))
        using (var border = new Pen(_p.Border))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }
        g.SmoothingMode = SmoothingMode.None;

        TextRenderer.DrawText(g, title, _tipTitleFont, new Point(x + pad, y + pad), _p.Text, flags);
        var rowY = y + pad + _tipTitleFont.Height + S(6);
        foreach (var r in rows)
        {
            TextRenderer.DrawText(g, r.Label, _tipFont, new Point(x + pad, rowY), r.Color, flags);
            if (r.Value.Length > 0)
                TextRenderer.DrawText(g, r.Value, _tipFont,
                    new Rectangle(x + pad, rowY, width - pad * 2, _tipFont.Height), _p.Text, flags | TextFormatFlags.Right);
            rowY += lineHeight;
        }
    }

    /// <summary>"Runde" Schrittweite in 1024er-Einheiten, sodass 2-4 Gitterlinien entstehen.</summary>
    private static (long Step, int Count) ComputeScale(long max)
    {
        if (max <= 0)
            max = 1024 * 1024;

        var raw = max / 4.0;
        var unit = 1.0;
        while (raw >= unit * 1024)
            unit *= 1024;

        double[] nice = { 1, 2, 2.5, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1024 };
        var mantissa = nice.First(v => v >= raw / unit);
        var step = Math.Max(1L, (long)Math.Ceiling(mantissa * unit));
        var count = Math.Max(1, (int)Math.Ceiling(max / (double)step));
        return (step, count);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _tipFont.Dispose();
            _tipTitleFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
