using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Inspector panel for Stage 2 (Saliency, SaliencyCut/GrabCut/Mask, and Saliency Distance Field).
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
    /// Initializes a new instance of <see cref="SaliencyInspectorView"/>.
    /// </summary>
    public SaliencyInspectorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the command invoked when resetting Stage 2 to the active baseline.
    /// </summary>
    public ICommand? ResetStageCommand
    {
        get => GetValue(ResetStageCommandProperty);
        set => SetValue(ResetStageCommandProperty, value);
    }
}
