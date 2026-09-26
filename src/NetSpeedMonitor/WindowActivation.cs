using System.Runtime.InteropServices;

namespace NetSpeedMonitor;

/// <summary>
/// Holt eigene Fenster zuverlaessig in den Vordergrund. Ein Klick auf das nicht aktivierbare
/// Panel gibt dem Prozess kein Vordergrund-Recht, Activate() allein wird dann von Windows
/// ignoriert - daher kurz an den Eingabe-Thread des aktuellen Vordergrundfensters koppeln.
/// </summary>
public static class WindowActivation
{
    public static void ForceForeground(Form form)
    {
        try
        {
            var foreground = GetForegroundWindow();
            var foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
            var currentThread = GetCurrentThreadId();

            if (foreground != IntPtr.Zero && foregroundThread != currentThread
                && AttachThreadInput(currentThread, foregroundThread, true))
            {
                try
                {
                    SetForegroundWindow(form.Handle);
                }
                finally
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
        }
        catch
        {
            // Faellt auf das normale Activate() zurueck.
        }
        form.Activate();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
