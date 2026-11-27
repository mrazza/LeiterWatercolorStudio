using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Inspector panel for Stage 3 (Saliency, SaliencyCut/GrabCut/Mask, and Saliency Distance Field).
/// </summary>
public partial class SaliencyInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<SaliencyInspectorView, ICommand?>(nameof(ResetStageCommand), null);

    /// <summary>
    /// Gets all <see cref="GridConnectivity"/> values for EGBI and mask connectivity selectors.
    /// </summary>
    public static IReadOnlyList<GridConnectivity> GridConnectivities { get; } = Enum.GetValues<GridConnectivity>();

    /// <summary>
    /// Gets all <see cref="SaliencyThresholdMethod"/> values for saliency mask thresholding.
    /// </summary>
    public static IReadOnlyList<SaliencyThresholdMethod> ThresholdMethods { get; } = Enum.GetValues<SaliencyThresholdMethod>();

    /// <summary>
    /// Gets all <see cref="ComponentRetentionPolicy"/> values for connected-component filtering.
    /// </summary>
    public static IReadOnlyList<ComponentRetentionPolicy> RetentionPolicies { get; } = Enum.GetValues<ComponentRetentionPolicy>();

    /// <summary>
    /// Converts a <see cref="SaliencyThresholdMethod"/> to whether <see cref="SaliencyThresholdMethod.Fixed"/> is active.
    /// </summary>
    public static IValueConverter IsFixedThresholdMethodConverter { get; } =
        new FuncValueConverter<SaliencyThresholdMethod, bool>(m => m == SaliencyThresholdMethod.Fixed);

    /// <summary>
    /// Converts a <see cref="SaliencyThresholdMethod"/> to visual opacity for the Fixed Threshold slider.
    /// </summary>
    public static IValueConverter FixedThresholdOpacityConverter { get; } =
        new FuncValueConverter<SaliencyThresholdMethod, double>(m => m == SaliencyThresholdMethod.Fixed ? 1.0 : 0.45);

    /// <summary>
    /// Converts a <see cref="ComponentRetentionPolicy"/> to whether <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/> is active.
    /// </summary>
    public static IValueConverter IsMinimumAreaRetentionConverter { get; } =
        new FuncValueConverter<ComponentRetentionPolicy, bool>(p => p == ComponentRetentionPolicy.MinimumAreaFraction);

    /// <summary>
    /// Converts a <see cref="ComponentRetentionPolicy"/> to visual opacity for the Min Area Fraction slider.
    /// </summary>
    public static IValueConverter MinimumAreaOpacityConverter { get; } =
        new FuncValueConverter<ComponentRetentionPolicy, double>(p => p == ComponentRetentionPolicy.MinimumAreaFraction ? 1.0 : 0.45);

    /// <summary>
    /// Initializes a new instance of <see cref="SaliencyInspectorView"/>.
    /// </summary>
    public SaliencyInspectorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the command invoked when resetting Stage 3 to the active baseline.
    /// </summary>
    public ICommand? ResetStageCommand
    {
        get => GetValue(ResetStageCommandProperty);
        set => SetValue(ResetStageCommandProperty, value);
    }
}
