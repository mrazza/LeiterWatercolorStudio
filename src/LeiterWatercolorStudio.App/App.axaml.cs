using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LeiterWatercolorStudio.App.Services;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.App.Views;

namespace LeiterWatercolorStudio.App;

/// <summary>
/// Application entry point configuring the studio theme and desktop main window lifetime.
/// </summary>
public partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow? mainWindow = null;
            var fileDialogs = new AvaloniaFileDialogService(() => mainWindow);
            var viewModel = new MainWindowViewModel(fileDialogs);

            mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
