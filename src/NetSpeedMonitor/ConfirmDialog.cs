namespace NetSpeedMonitor;

/// <summary>Themen-faehiger Ersatz fuer eine Ja/Nein-MessageBox.</summary>
public sealed class ConfirmDialog : Form
{
    private readonly ThemePalette _palette = ThemeHelper.GetAppPalette();

    private ConfirmDialog(string title, string heading, string message, string confirmText)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20, 16, 20, 16);
        ThemeHelper.ApplyAppIcon(this);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
        layout.Controls.Add(new Label
        {
            Text = heading,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11f),
            Margin = new Padding(0, 0, 0, 8)
        });
        layout.Controls.Add(new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            Margin = new Padding(0, 0, 0, 20)
        });

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        var cancel = new Button { Text = "Abbrechen", AutoSize = true, MinimumSize = new Size(92, 30), DialogResult = DialogResult.Cancel, Margin = Padding.Empty };
        var confirm = new Button { Text = confirmText, AutoSize = true, MinimumSize = new Size(92, 30), DialogResult = DialogResult.OK, Margin = new Padding(0, 0, 8, 0) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(confirm);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        CancelButton = cancel;
        // Bei destruktiven Aktionen liegt der Fokus bewusst auf "Abbrechen".
        ActiveControl = cancel;

        ThemeHelper.ApplyControlTheme(this, _palette);
        ThemeHelper.StylePrimaryButton(confirm, _palette);
        ResumeLayout(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ThemeHelper.ApplyTitleBarTheme(this, _palette.IsDark);
    }

    public static bool Ask(string title, string heading, string message, string confirmText)
    {
        using var dialog = new ConfirmDialog(title, heading, message, confirmText);
        return dialog.ShowDialog() == DialogResult.OK;
    }
}
