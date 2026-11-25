using System.Runtime.InteropServices;

namespace LeiterWatercolorStudio.App.Cli;

/// <summary>
/// Attaches Windows GUI-subsystem (<c>WinExe</c>) invocations to their parent console when running headlessly.
/// </summary>
internal static class WindowsConsoleHelper
{
    private const int AttachParentProcess = -1;

    public static void TryAttachParentConsole()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (!Console.IsOutputRedirected && !Console.IsErrorRedirected)
            {
                _ = AttachConsole(AttachParentProcess);
            }
        }
        catch
        {
            // Ignore console attachment failures in non-console or restricted hosts.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}
