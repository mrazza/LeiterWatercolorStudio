using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(LeiterWatercolorStudio.App.Tests.TestAppBuilder))]

namespace LeiterWatercolorStudio.App.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<global::LeiterWatercolorStudio.App.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
