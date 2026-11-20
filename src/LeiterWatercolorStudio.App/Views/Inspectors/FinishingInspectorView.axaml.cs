using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Leiter.Algorithms;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Stage 8 (Leiter Stage 6) inspector view for Edge Darkening, Turbulence Flow, Tone Curve, Paper Granulation, and FXAA.
/// </summary>
public partial class FinishingInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<FinishingInspectorView, ICommand?>(nameof(ResetStageCommand));

    /// <summary>
    /// Gets all supported <see cref="GradientOperator"/> values for binding to the gradient operator selector.
    /// </summary>
    public static IReadOnlyList<GradientOperator> AvailableGradientOperators { get; } = Enum.GetValues<GradientOperator>();

    /// <summary>
    /// Initializes a new instance of <see cref="FinishingInspectorView"/>.
    /// </summary>
    public FinishingInspectorView()
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
}
