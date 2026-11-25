using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using LeiterWatercolorStudio.App.Cli;
using LeiterWatercolorStudio.Core.Cli;

namespace LeiterWatercolorStudio.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (CliArgumentParser.ShouldLaunchGui(args))
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        WindowsConsoleHelper.TryAttachParentConsole();

        using var cts = new CancellationTokenSource();
        int sigtermReceived = 0;

        void CancelSafely()
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            CancelSafely();
        };

        Console.CancelKeyPress += cancelHandler;
        PosixSignalRegistration? sigtermRegistration = !OperatingSystem.IsWindows()
            ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                Volatile.Write(ref sigtermReceived, 1);
                CancelSafely();
            })
            : null;

        try
        {
            int exitCode = CliApplication
                .RunAsync(args, Console.Out, Console.Error, ResolveVersion(), cts.Token)
                .GetAwaiter()
                .GetResult();

            if (exitCode == CliExitCodes.Cancelled && Volatile.Read(ref sigtermReceived) != 0)
            {
                return CliExitCodes.Terminated;
            }

            return exitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            sigtermRegistration?.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static string ResolveVersion()
    {
        Assembly assembly = typeof(Program).Assembly;
        string? infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            int plusIndex = infoVersion.IndexOf('+');
            return plusIndex > 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
