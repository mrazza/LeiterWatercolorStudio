using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Parameters.Stages;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Stage 6 (Leiter Stage 4) inspector view for wet-in-wet edge bleeding parameters and boundary statistics.
/// </summary>
public partial class WetInWetInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<WetInWetInspectorView, ICommand?>(nameof(ResetStageCommand));

    /// <summary>
    /// Identifies the <see cref="StatsText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> StatsTextProperty =
        AvaloniaProperty.Register<WetInWetInspectorView, string?>(nameof(StatsText));

    /// <summary>
    /// Gets all supported <see cref="WetInWetMode"/> values for binding to the mode selector.
    /// </summary>
    public static IReadOnlyList<WetInWetMode> AvailableModes { get; } = Enum.GetValues<WetInWetMode>();

    /// <summary>
    /// Initializes a new instance of <see cref="WetInWetInspectorView"/>.
    /// </summary>
    public WetInWetInspectorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the command invoked when the user clicks the stage reset button.
    /// </summary>
    public ICommand? ResetStageCommand
    {
        get => GetValue(ResetStageCommandProperty);
        set => SetValue(ResetStageCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the live qualifying boundary statistics text displayed in the inspector.
    /// </summary>
    public string? StatsText
    {
        get => GetValue(StatsTextProperty);
        set => SetValue(StatsTextProperty, value);
    }

    private void OnRandomizeSeedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WetInWetStageViewModel vm)
        {
            vm.RandomizeSeed();
        }
    }
}
