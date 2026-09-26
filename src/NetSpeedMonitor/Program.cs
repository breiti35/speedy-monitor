namespace NetSpeedMonitor;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Eine zweite Instanz wuerde ein zweites, ueberlappendes Panel in die Taskleiste legen.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\SpeedyMonitor_SingleInstance", out var createdNew);
        if (!createdNew)
            return;

        AppPaths.MigrateLegacyDataFolder();

        // Hintergrund-Tool: transiente UIA/COM-Fehler im UI-Thread nur protokollieren, nicht beenden.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Error("Unbehandelte Ausnahme im UI-Thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error($"Unbehandelte Ausnahme (terminierend: {e.IsTerminating})", e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
