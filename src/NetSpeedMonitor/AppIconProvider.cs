namespace NetSpeedMonitor;

/// <summary>Liefert das eingebettete Anwendungs-Icon (gecacht) fuer Formulare.</summary>
public static class AppIconProvider
{
    private static Icon? _icon;

    public static Icon Get()
    {
        if (_icon is not null)
            return _icon;

        using var stream = typeof(AppIconProvider).Assembly.GetManifestResourceStream("NetSpeedMonitor.AppIcon.ico");
        _icon = stream is not null ? new Icon(stream) : SystemIcons.Application;
        return _icon;
    }
}
