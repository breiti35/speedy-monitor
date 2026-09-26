using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NetSpeedMonitor;

/// <summary>Farbsatz fuer die eigenen Fenster/Menues im Windows-11-Stil.</summary>
public sealed record ThemePalette(
    bool IsDark,
    Color Background,
    Color Surface,
    Color Border,
    Color Hover,
    Color Text,
    Color SubtleText,
    Color DisabledText,
    Color Accent,
    Color AccentText,
    Color Upload,
    Color Download)
{
    public static readonly ThemePalette Dark = new(
        IsDark: true,
        Background: Color.FromArgb(32, 32, 32),
        Surface: Color.FromArgb(44, 44, 44),
        Border: Color.FromArgb(64, 64, 64),
        Hover: Color.FromArgb(58, 58, 58),
        Text: Color.FromArgb(255, 255, 255),
        SubtleText: Color.FromArgb(160, 160, 160),
        DisabledText: Color.FromArgb(110, 110, 110),
        Accent: Color.FromArgb(96, 205, 255),
        AccentText: Color.FromArgb(0, 0, 0),
        Upload: Color.FromArgb(255, 152, 0),
        Download: Color.FromArgb(0, 205, 120));

    // Upload/Download etwas dunkler, damit der Text auf hellem Grund lesbar bleibt.
    public static readonly ThemePalette Light = new(
        IsDark: false,
        Background: Color.FromArgb(243, 243, 243),
        Surface: Color.FromArgb(251, 251, 251),
        Border: Color.FromArgb(222, 222, 222),
        Hover: Color.FromArgb(234, 234, 234),
        Text: Color.FromArgb(26, 26, 26),
        SubtleText: Color.FromArgb(96, 96, 96),
        DisabledText: Color.FromArgb(160, 160, 160),
        Accent: Color.FromArgb(0, 95, 184),
        AccentText: Color.FromArgb(255, 255, 255),
        Upload: Color.FromArgb(214, 118, 0),
        Download: Color.FromArgb(0, 150, 88));
}

/// <summary>Liest den Windows-Hell/Dunkel-Modus und passt Fenster/Steuerelemente daran an.</summary>
public static class ThemeHelper
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;

    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    /// <summary>Hell/Dunkel der Taskleiste und Shell-Oberflaechen.</summary>
    public static bool IsSystemLightTheme() => ReadFlag("SystemUsesLightTheme", defaultValue: false);

    /// <summary>Hell/Dunkel fuer normale App-Fenster (Einstellungen, Statistik).</summary>
    public static bool IsAppsLightTheme() => ReadFlag("AppsUseLightTheme", defaultValue: true);

    public static ThemePalette GetAppPalette() => IsAppsLightTheme() ? ThemePalette.Light : ThemePalette.Dark;

    public static void ApplyTitleBarTheme(Form form, bool dark) =>
        SetDwmInt(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, dark ? 1 : 0);

    /// <summary>Windows-11-Rundung (inkl. DWM-Rahmen); auf Windows 10 wirkungslos.</summary>
    public static void ApplyRoundedCorners(IntPtr hwnd, int preference, Color border)
    {
        SetDwmInt(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, preference);
        SetDwmInt(hwnd, DWMWA_BORDER_COLOR, ColorTranslator.ToWin32(border));
    }

    public static bool SupportsRoundedCorners => Environment.OSVersion.Version.Build >= 22000;

    public static void ApplyAppIcon(Form form)
    {
        try
        {
            form.Icon = AppIconProvider.Get();
        }
        catch
        {
            // Ohne Icon ist das Fenster trotzdem voll benutzbar.
        }
    }

    /// <summary>Faerbt einen Dialog samt aller Unter-Steuerelemente nach der Palette ein.</summary>
    public static void ApplyControlTheme(Control root, ThemePalette p)
    {
        root.BackColor = p.Background;
        root.ForeColor = p.Text;

        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.UseVisualStyleBackColor = false;
                    b.BackColor = p.Surface;
                    b.ForeColor = p.Text;
                    b.FlatAppearance.BorderColor = p.Border;
                    b.FlatAppearance.MouseOverBackColor = p.Hover;
                    b.FlatAppearance.MouseDownBackColor = p.Border;
                    break;
                case ComboBox cb:
                    cb.BackColor = p.Surface;
                    cb.ForeColor = p.Text;
                    ApplyNativeTheme(cb, p.IsDark ? "DarkMode_CFD" : "Explorer");
                    break;
                case NumericUpDown nud:
                    nud.BackColor = p.Surface;
                    nud.ForeColor = p.Text;
                    nud.BorderStyle = BorderStyle.FixedSingle;
                    if (nud.Controls.Count > 0)
                        ApplyNativeTheme(nud.Controls[0], p.IsDark ? "DarkMode_Explorer" : "Explorer");
                    break;
                case TextBox tb:
                    tb.BackColor = p.Surface;
                    tb.ForeColor = p.Text;
                    break;
                case CheckBox chk:
                    chk.BackColor = p.Background;
                    chk.ForeColor = p.Text;
                    // Der Standard-Checkbox-Kasten bleibt sonst auch im Dunkelmodus hell.
                    chk.FlatStyle = p.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                    chk.FlatAppearance.BorderColor = p.SubtleText;
                    chk.FlatAppearance.CheckedBackColor = p.Surface;
                    chk.FlatAppearance.MouseOverBackColor = p.Hover;
                    break;
                default:
                    ApplyControlTheme(c, p);
                    break;
            }
        }
    }

    /// <summary>Hervorgehobene Primaer-Schaltflaeche (OK) in Akzentfarbe.</summary>
    public static void StylePrimaryButton(Button b, ThemePalette p)
    {
        b.BackColor = p.Accent;
        b.ForeColor = p.AccentText;
        b.FlatAppearance.BorderColor = p.Accent;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(p.Accent, 0.2f);
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(p.Accent, 0.05f);
    }

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void ApplyNativeTheme(Control c, string theme)
    {
        if (c.IsHandleCreated)
            SetWindowThemeSafe(c.Handle, theme);
        else
            c.HandleCreated += (_, _) => SetWindowThemeSafe(c.Handle, theme);
    }

    private static void SetWindowThemeSafe(IntPtr hwnd, string theme)
    {
        try
        {
            SetWindowTheme(hwnd, theme, null);
        }
        catch
        {
            // Rein kosmetisch.
        }
    }

    private static void SetDwmInt(IntPtr hwnd, int attr, int value)
    {
        try
        {
            DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int));
        }
        catch
        {
            // Aeltere Windows-Builds ohne dieses Attribut - bleibt einfach beim Standard.
        }
    }

    private static bool ReadFlag(string name, bool defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int v ? v != 0 : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? pszSubAppName, string? pszSubIdList);
}
