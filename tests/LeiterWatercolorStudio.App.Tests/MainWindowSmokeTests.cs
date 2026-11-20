using Avalonia.Headless.XUnit;
using LeiterWatercolorStudio.App.Views;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class MainWindowSmokeTests
{
    [AvaloniaFact]
    public void MainWindow_CanInstantiate_InHeadlessSession()
    {
        var window = new MainWindow();
        Assert.Equal("Leiter Watercolor Studio", window.Title);
    }
}
