using System;
using System.Reflection;
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
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            return CliApplication
                .RunAsync(args, Console.Out, Console.Error, ResolveVersion(), cts.Token)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
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
