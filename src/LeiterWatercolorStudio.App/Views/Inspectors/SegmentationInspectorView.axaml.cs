using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Leiter.Algorithms.DataStructures;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Inspector panel for Stage 3a (CIELAB Mean-Shift Filtering and Segmentation).
/// </summary>
public partial class SegmentationInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<SegmentationInspectorView, ICommand?>(nameof(ResetStageCommand), null);

    /// <summary>
    /// Identifies the <see cref="StatsText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> StatsTextProperty =
        AvaloniaProperty.Register<SegmentationInspectorView, string?>(nameof(StatsText), null);

    /// <summary>
    /// Gets all <see cref="GridConnectivity"/> values for segmentation clustering and pruning.
    /// </summary>
    public static IReadOnlyList<GridConnectivity> GridConnectivities { get; } = Enum.GetValues<GridConnectivity>();

    /// <summary>
    /// Initializes a new instance of <see cref="SegmentationInspectorView"/>.
    /// </summary>
    public SegmentationInspectorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the command invoked when resetting Stage 3a to the active baseline.
    /// </summary>
    public ICommand? ResetStageCommand
    {
        get => GetValue(ResetStageCommandProperty);
        set => SetValue(ResetStageCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the live segmentation statistics readout (e.g., region and boundary counts).
    /// </summary>
    public string? StatsText
    {
        get => GetValue(StatsTextProperty);
        set => SetValue(StatsTextProperty, value);
    }
}
