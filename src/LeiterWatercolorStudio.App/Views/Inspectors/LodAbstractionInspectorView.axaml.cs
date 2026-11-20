using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Stage 5 (Leiter Stage 3b) inspector view for saliency-guided Level-of-Detail abstraction parameters.
/// </summary>
public partial class LodAbstractionInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<LodAbstractionInspectorView, ICommand?>(nameof(ResetStageCommand));

    /// <summary>
    /// Initializes a new instance of <see cref="LodAbstractionInspectorView"/>.
    /// </summary>
    public LodAbstractionInspectorView()
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
