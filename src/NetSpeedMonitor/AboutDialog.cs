using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace NetSpeedMonitor;

public sealed class AboutDialog : Form
{
    private static readonly string DataFolder = AppPaths.DataFolder;

    private readonly ThemePalette _palette = ThemeHelper.GetAppPalette();

    public AboutDialog()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        Text = "Über Speedy Monitor";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(20, 16, 20, 16);
        ThemeHelper.ApplyAppIcon(this);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
        layout.Controls.Add(new Label
        {
            Text = "Speedy Monitor",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14f),
            Margin = new Padding(0, 0, 0, 2)
        });
        var versionLabel = new Label { Text = $"Version {version}", AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
        layout.Controls.Add(versionLabel);
        layout.Controls.Add(new Label
        {
            Text = "Zeigt die aktuelle Upload- und Download-Geschwindigkeit direkt in der Taskleiste an – "
                 + "ein Nachbau des klassischen NetSpeedMonitor für Windows 11. Klick auf das Panel öffnet "
                 + "die Statistik, Doppelklick die Einstellungen, Rechtsklick das Menü.",
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            Margin = new Padding(0, 0, 0, 16)
        });
        var folderCaption = new Label { Text = "Datenordner (Einstellungen und Statistik):", AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        layout.Controls.Add(folderCaption);
        layout.Controls.Add(new Label { Text = DataFolder, AutoSize = true, MaximumSize = new Size(400, 0), Margin = new Padding(0, 0, 0, 20) });

        var buttons = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 3,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = Padding.Empty
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var openFolder = new Button { Text = "Ordner öffnen", AutoSize = true, MinimumSize = new Size(92, 30), Padding = new Padding(8, 0, 8, 0), Margin = Padding.Empty };
        var close = new Button { Text = "Schließen", AutoSize = true, MinimumSize = new Size(92, 30), Margin = Padding.Empty };
        openFolder.Click += (_, _) => OpenDataFolder();
        close.Click += (_, _) => Close();
        buttons.Controls.Add(openFolder, 0, 0);
        buttons.Controls.Add(close, 2, 0);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;

        ThemeHelper.ApplyControlTheme(this, _palette);
        ThemeHelper.StylePrimaryButton(close, _palette);
        versionLabel.ForeColor = _palette.SubtleText;
        folderCaption.ForeColor = _palette.SubtleText;
        ResumeLayout(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ThemeHelper.ApplyTitleBarTheme(this, _palette.IsDark);
    }

    private static void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DataFolder}\"") { UseShellExecute = true });
        }
        catch
        {
            // Explorer nicht startbar - nichts weiter zu tun, der Pfad steht ja im Dialog.
        }
    }
}
