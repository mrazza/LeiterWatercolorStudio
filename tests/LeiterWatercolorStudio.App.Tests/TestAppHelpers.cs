using Avalonia.Threading;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.Core.Pipeline;
using LeiterWatercolorStudio.Core.Pipeline.Events;

namespace LeiterWatercolorStudio.App.Tests;

internal static class TestAppHelpers
{
    public static string FindExampleInputPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "extern", "Leiter", "examples", "input.png");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate extern/Leiter/examples/input.png from test base directory.");
    }

    public static void PumpUi()
    {
        Dispatcher.UIThread.RunJobs();
    }

    public static async Task WaitForRenderAsync(
        MainWindowViewModel vm,
        Func<Task> triggerAsync,
        int timeoutMs = 120000)
    {
        var completedTcs = new TaskCompletionSource<RenderCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCompleted(object? sender, RenderCompletedEventArgs e)
        {
            // Queue behind MainWindowViewModel's own UI-thread dispatch so stage cards and bitmaps are updated first.
            Dispatcher.UIThread.Post(() => completedTcs.TrySetResult(e));
        }

        void OnFailed(object? sender, RenderFailedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => completedTcs.TrySetException(e.Exception));
        }

        vm.Engine.RenderCompleted += OnCompleted;
        vm.Engine.RenderFailed += OnFailed;
        try
        {
            await triggerAsync();

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using CancellationTokenRegistration reg = timeoutCts.Token.Register(
                () => completedTcs.TrySetException(new TimeoutException($"Timed out after {timeoutMs} ms waiting for pipeline render.")));

            await completedTcs.Task;
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            vm.Engine.RenderCompleted -= OnCompleted;
            vm.Engine.RenderFailed -= OnFailed;
        }
    }

    public static Task WaitForRenderAsync(
        MainWindowViewModel vm,
        Action trigger,
        int timeoutMs = 120000)
    {
        return WaitForRenderAsync(vm, () =>
        {
            trigger();
            return Task.CompletedTask;
        }, timeoutMs);
    }
}
