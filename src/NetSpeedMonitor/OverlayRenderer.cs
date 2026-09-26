using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NetSpeedMonitor;

internal readonly record struct OverlayContent(string UpValue, string UpUnit, string DownValue, string DownUnit);

/// <summary>
/// Zeichnet die zwei Zeilen (Upload oben, Download unten) direkt auf transparenten Grund:
/// Dreieck | Zahl rechtsbuendig in fester Spalte | Einheit linksbuendig in fester Spalte.
/// Alle Masse haengen nur von der Taskleistenhoehe ab, nie vom aktuellen Text.
/// </summary>
internal sealed class OverlayRenderer : IDisposable
{
    // Worst-Case ueber alle Einheiten; noch laengere Werte werden horizontal gestaucht statt
    // die Spalte (und damit das Panel) zu verbreitern.
    private static readonly string[] ValueTemplates = { "9999,9", "999,99", "8888,8", "888,88" };
    private static readonly string[] UnitTemplates = { "B/s", "KB/s", "MB/s", "kbit/s", "Mbit/s" };

    private readonly Font _valueFont;
    private readonly Font _unitFont;
    private readonly StringFormat _format;

    private readonly float _capHeight;
    private readonly float _valueAscent;
    private readonly float _unitAscent;
    private readonly int _arrowLeft;
    private readonly int _arrowWidth;
    private readonly int _arrowHeight;
    private readonly int _valueRight;
    private readonly int _valueColumnWidth;
    private readonly int _unitLeft;
    private readonly int _baselineUp;
    private readonly int _baselineDown;

    private OverlayPalette? _palette;

    public int Width { get; }
    public int Height { get; }

    public OverlayRenderer(int taskbarHeight)
    {
        Height = Math.Max(1, taskbarHeight);

        var valuePx = Math.Clamp(Height / 2f * 0.66f, 12f, 24f);
        _valueFont = new Font("Segoe UI", valuePx, FontStyle.Bold, GraphicsUnit.Pixel);
        _unitFont = new Font("Segoe UI Semibold", valuePx * 0.8f, FontStyle.Regular, GraphicsUnit.Pixel);

        _format = (StringFormat)StringFormat.GenericTypographic.Clone();
        _format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces;

        _valueAscent = Ascent(_valueFont);
        _unitAscent = Ascent(_unitFont);
        _capHeight = valuePx * 0.7f; // Versalhoehe Segoe UI

        using var probe = new Bitmap(1, 1);
        using var g = Graphics.FromImage(probe);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        _valueColumnWidth = (int)Math.Ceiling(ValueTemplates.Max(t => Measure(g, t, _valueFont)));
        var unitColumnWidth = (int)Math.Ceiling(UnitTemplates.Max(t => Measure(g, t, _unitFont)));

        var padding = (int)Math.Round(valuePx * 0.3f);
        _arrowHeight = Math.Max(5, (int)Math.Round(_capHeight * 0.72f));
        _arrowWidth = Math.Max(6, (int)Math.Round(_arrowHeight * 1.2f));
        var arrowGap = (int)Math.Round(valuePx * 0.3f);
        var unitGap = (int)Math.Round(_unitFont.Size * 0.3f);

        _arrowLeft = padding;
        _valueRight = _arrowLeft + _arrowWidth + arrowGap + _valueColumnWidth;
        _unitLeft = _valueRight + unitGap;
        Width = _unitLeft + unitColumnWidth + padding;

        // Zeilen symmetrisch um die Mitte; Abstand an die Schrift gekoppelt, damit bei hohen
        // Taskleisten nicht eine Zeile am oberen und eine am unteren Rand klebt.
        var rowDistance = Math.Min(Height / 2f, valuePx * 1.2f);
        _baselineUp = (int)Math.Round(Height / 2f - rowDistance / 2f + _capHeight / 2f);
        _baselineDown = (int)Math.Round(Height / 2f + rowDistance / 2f + _capHeight / 2f);
    }

    public void Render(Graphics g, OverlayContent content, bool lightTheme)
    {
        if (_palette is null || _palette.Light != lightTheme)
        {
            _palette?.Dispose();
            _palette = new OverlayPalette(lightTheme);
        }

        // Alpha 1 statt 0: voll transparente Pixel eines Layered Windows sind klick-durchlaessig,
        // so bleibt die ganze Panelflaeche fuer Klicks/Tooltip erreichbar, ohne sichtbar zu sein.
        g.Clear(Color.FromArgb(1, 0, 0, 0));
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        DrawRow(g, up: true, content.UpValue, content.UpUnit, _baselineUp, _palette.Upload);
        DrawRow(g, up: false, content.DownValue, content.DownUnit, _baselineDown, _palette.Download);
    }

    private void DrawRow(Graphics g, bool up, string value, string unit, int baseline, Brush accent)
    {
        var palette = _palette!;

        using (var arrow = BuildArrow(up, baseline - _capHeight / 2f))
        {
            if (palette.Shadow is not null)
            {
                g.TranslateTransform(0, 1);
                g.FillPath(palette.Shadow, arrow);
                g.ResetTransform();
            }
            g.FillPath(accent, arrow);
        }

        var valueWidth = Measure(g, value, _valueFont);
        var valueTop = baseline - _valueAscent;
        if (valueWidth <= _valueColumnWidth)
        {
            DrawText(g, value, _valueFont, _valueRight - valueWidth, valueTop, accent);
        }
        else
        {
            var state = g.Save();
            g.TranslateTransform(_valueRight, 0);
            g.ScaleTransform(_valueColumnWidth / valueWidth, 1f);
            DrawText(g, value, _valueFont, -valueWidth, valueTop, accent);
            g.Restore(state);
        }

        DrawText(g, unit, _unitFont, _unitLeft, baseline - _unitAscent, palette.Unit);
    }

    private void DrawText(Graphics g, string text, Font font, float x, float y, Brush brush)
    {
        if (_palette!.Shadow is not null)
            g.DrawString(text, font, _palette.Shadow, x, y + 1, _format);
        g.DrawString(text, font, brush, x, y, _format);
    }

    private GraphicsPath BuildArrow(bool up, float centerY)
    {
        float left = _arrowLeft;
        float right = _arrowLeft + _arrowWidth;
        var top = (float)Math.Round(centerY - _arrowHeight / 2f);
        var bottom = top + _arrowHeight;
        var mid = (left + right) / 2f;

        var path = new GraphicsPath();
        if (up)
            path.AddPolygon(new[] { new PointF(left, bottom), new PointF(right, bottom), new PointF(mid, top) });
        else
            path.AddPolygon(new[] { new PointF(left, top), new PointF(right, top), new PointF(mid, bottom) });
        return path;
    }

    private float Measure(Graphics g, string text, Font font) =>
        g.MeasureString(text, font, PointF.Empty, _format).Width;

    private static float Ascent(Font font)
    {
        var family = font.FontFamily;
        return font.Size * family.GetCellAscent(font.Style) / family.GetEmHeight(font.Style);
    }

    public void Dispose()
    {
        _valueFont.Dispose();
        _unitFont.Dispose();
        _format.Dispose();
        _palette?.Dispose();
    }

    private sealed class OverlayPalette : IDisposable
    {
        public bool Light { get; }
        public Brush Upload { get; }
        public Brush Download { get; }
        public Brush Unit { get; }
        public Brush? Shadow { get; }

        public OverlayPalette(bool light)
        {
            Light = light;
            if (light)
            {
                Upload = new SolidBrush(Color.FromArgb(0xC2, 0x5E, 0x00));
                Download = new SolidBrush(Color.FromArgb(0x00, 0x7A, 0x45));
                Unit = new SolidBrush(Color.FromArgb(0x50, 0x50, 0x50));
            }
            else
            {
                Upload = new SolidBrush(Color.FromArgb(0xFF, 0x98, 0x00));
                Download = new SolidBrush(Color.FromArgb(0x00, 0xCD, 0x78));
                Unit = new SolidBrush(Color.FromArgb(0xD0, 0xD4, 0xD4));
                // Dezenter Schatten nur im Dunkelmodus: hebt helle Schrift von Akzentfarben
                // (z.B. Tuerkis) ab; auf heller Taskleiste wuerde er nur verschmieren.
                Shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
            }
        }

        public void Dispose()
        {
            Upload.Dispose();
            Download.Dispose();
            Unit.Dispose();
            Shadow?.Dispose();
        }
    }
}
