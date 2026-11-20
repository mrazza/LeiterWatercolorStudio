using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LeiterWatercolorStudio.Core.Parameters.Stages;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Stage 7 (Leiter Stage 5) inspector view for hand tremor boundary perturbation, paper color/gaps, and noise fields.
/// </summary>
public partial class HandTremorInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<HandTremorInspectorView, ICommand?>(nameof(ResetStageCommand));

    /// <summary>
    /// Initializes a new instance of <see cref="HandTremorInspectorView"/>.
    /// </summary>
    public HandTremorInspectorView()
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

    private void OnPaperColorHexKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not HandTremorStageViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.PaperColorHex = PART_PaperColorHexTextBox.Text ?? string.Empty;
            PART_PaperColorHexTextBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            PART_PaperColorHexTextBox.Text = vm.PaperColorHex;
            PART_PaperColorHexTextBox.SelectAll();
            e.Handled = true;
        }
    }
}
