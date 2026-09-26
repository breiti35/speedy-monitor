using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace NetSpeedMonitor;

/// <summary>Menuepunkt, der als Optionsfeld (Punkt statt Haken) gezeichnet wird.</summary>
public sealed class RadioMenuItem : ToolStripMenuItem
{
    public RadioMenuItem(string text, EventHandler onClick) : base(text, null, onClick)
    {
    }
}

/// <summary>Nicht anklickbare Kopfzeile mit farbigem ↑/↓-Wert.</summary>
public sealed class SpeedHeaderItem : ToolStripMenuItem
{
    public string UpText { get; private set; } = "";
    public string DownText { get; private set; } = "";

    public SpeedHeaderItem()
    {
        Enabled = false;
    }

    public void SetValues(string up, string down)
    {
        UpText = $"↑ {up}";
        DownText = $"↓ {down}";
        Text = UpText + Gap + DownText;
    }

    public const string Gap = "     ";
}

/// <summary>
/// Kontextmenue-Darstellung im Windows-11-Stil: flache Flaeche, abgerundete Hover-Markierung,
/// grosszuegige Zeilenhoehe, dezente Trennlinien, selbst gezeichnete Haken/Pfeile.
/// </summary>
public sealed class MenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly ConditionalWeakTable<ToolStripDropDown, object> Hooked = new();
    private static readonly Font MenuFont = new("Segoe UI", 9f);
    private static readonly Font HeaderFont = new("Segoe UI Semibold", 9f);

    private readonly ThemePalette _p;

    public MenuRenderer(ThemePalette palette)
    {
        _p = palette;
        RoundedEdges = false;
    }

    /// <summary>Stil (rekursiv inkl. Untermenues) anwenden; mehrfacher Aufruf ist unkritisch.</summary>
    public static void Apply(ToolStripDropDownMenu menu, ThemePalette palette)
    {
        var scale = menu.DeviceDpi / 96f;
        int S(float v) => (int)Math.Round(v * scale);

        menu.Renderer = new MenuRenderer(palette);
        menu.Font = MenuFont;
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = true;
        menu.Padding = new Padding(S(2), S(4), S(2), S(4));
        menu.BackColor = palette.Surface;
        menu.ForeColor = palette.Text;

        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripSeparator)
            {
                item.AutoSize = false;
                item.Height = S(9);
                continue;
            }

            item.Padding = new Padding(0, S(7), S(8), S(7));
            item.ForeColor = palette.Text;
            if (item is SpeedHeaderItem)
                item.Font = HeaderFont;
            if (item is ToolStripMenuItem { HasDropDownItems: true } sub && sub.DropDown is ToolStripDropDownMenu dd)
                Apply(dd, palette);
        }

        if (!Hooked.TryGetValue(menu, out _))
        {
            Hooked.Add(menu, new object());
            menu.HandleCreated += (_, _) => ApplyWindowStyle(menu);
        }
        if (menu.IsHandleCreated)
            ApplyWindowStyle(menu);
    }

    private static void ApplyWindowStyle(ToolStripDropDown menu)
    {
        if (menu.Renderer is MenuRenderer r)
            ThemeHelper.ApplyRoundedCorners(menu.Handle, ThemeHelper.DWMWCP_ROUNDSMALL, r._p.Border);
    }

    private static int Scale(ToolStripItem item, float v) =>
        (int)Math.Round(v * ((item.Owner?.DeviceDpi ?? 96) / 96f));

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_p.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // Unter Windows 11 zeichnet DWM den (abgerundeten) Rahmen selbst.
        if (ThemeHelper.SupportsRoundedCorners)
            return;
        using var pen = new Pen(_p.Border);
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        if (item is SpeedHeaderItem)
        {
            // Kopfzeile aendert sich live bei offenem Menue - alten Text sicher uebermalen.
            using var clear = new SolidBrush(_p.Surface);
            e.Graphics.FillRectangle(clear, 0, 0, item.Width, item.Height);
            return;
        }
        if (!item.Enabled || !(item.Selected || item.Pressed))
            return;

        var inset = Scale(item, 4);
        var rect = new RectangleF(inset, 1, item.Width - inset * 2, item.Height - 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = ThemeHelper.RoundedRect(rect, Scale(item, 4));
        using var brush = new SolidBrush(_p.Hover);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item is SpeedHeaderItem header)
        {
            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            var upWidth = TextRenderer.MeasureText(e.Graphics, header.UpText + SpeedHeaderItem.Gap, e.TextFont,
                Size.Empty, TextFormatFlags.NoPadding).Width;
            var upRect = new Rectangle(e.TextRectangle.X, 0, upWidth, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, header.UpText, e.TextFont, upRect, _p.Upload, flags);
            var downRect = new Rectangle(e.TextRectangle.X + upWidth, 0, Math.Max(0, e.Item.Width - e.TextRectangle.X - upWidth), e.Item.Height);
            TextRenderer.DrawText(e.Graphics, header.DownText, e.TextFont, downRect, _p.Download, flags);
            return;
        }

        var color = e.Item.Enabled ? _p.Text : _p.DisabledText;
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, color, e.TextFormat);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        var cx = r.X + r.Width / 2f;
        var cy = r.Y + r.Height / 2f;
        var color = e.Item.Enabled ? _p.Text : _p.DisabledText;

        if (e.Item is RadioMenuItem)
        {
            var d = Scale(e.Item, 6);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, cx - d / 2f, cy - d / 2f, d, d);
            return;
        }

        var s = Scale(e.Item, 1);
        using var pen = new Pen(color, 1.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[]
        {
            new PointF(cx - 4.5f * s, cy + 0.2f * s),
            new PointF(cx - 1.5f * s, cy + 3.2f * s),
            new PointF(cx + 4.5f * s, cy - 3.3f * s)
        });
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ArrowRectangle;
        var s = e.Item is null ? 1 : Scale(e.Item, 1);
        var cx = r.X + r.Width / 2f;
        var cy = r.Y + r.Height / 2f;
        var color = e.Item?.Enabled == false ? _p.DisabledText : _p.Text;
        using var pen = new Pen(color, 1.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, new[]
        {
            new PointF(cx - 1.5f * s, cy - 3.5f * s),
            new PointF(cx + 2f * s, cy),
            new PointF(cx - 1.5f * s, cy + 3.5f * s)
        });
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var inset = Scale(e.Item, 4);
        var y = e.Item.Height / 2;
        using var pen = new Pen(_p.Border);
        e.Graphics.DrawLine(pen, inset, y, e.Item.Width - inset, y);
    }
}
