using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Leiter.Algorithms;
using LeiterWatercolorStudio.Core.Parameters.Stages;

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
    /// Identifies the <see cref="Histogram"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> HistogramProperty =
        AvaloniaProperty.Register<FinishingInspectorView, IReadOnlyList<double>?>(nameof(Histogram));

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
        PART_ToneCurveEditor.Histogram = Histogram;
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
    /// Gets or sets the normalized luminance histogram bins rendered behind <see cref="Controls.ToneCurveEditor"/>.
    /// </summary>
    public IReadOnlyList<double>? Histogram
    {
        get => GetValue(HistogramProperty);
        set => SetValue(HistogramProperty, value);
    }

    /// <summary>
    /// Expands the matching Finishing sub-stage card, collapses sibling cards for clarity,
    /// and scrolls the requested section into view.
    /// </summary>
    /// <param name="sectionKey">
    /// Section identifier (<c>EdgeDarkening</c>, <c>TurbulenceFlow</c>, <c>ToneCurve</c>, <c>PaperGranulation</c>, or <c>Fxaa</c>).
    /// </param>
    public void FocusSection(string sectionKey)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return;
        }

        Expander? target = sectionKey.Trim() switch
        {
            var s when s.Equals("EdgeDarkening", StringComparison.OrdinalIgnoreCase) => EdgeDarkeningExpander,
            var s when s.Equals("TurbulenceFlow", StringComparison.OrdinalIgnoreCase) => TurbulenceFlowExpander,
            var s when s.Equals("ToneCurve", StringComparison.OrdinalIgnoreCase) => ToneCurveExpander,
            var s when s.Equals("PaperGranulation", StringComparison.OrdinalIgnoreCase) => PaperGranulationExpander,
            var s when s.Equals("Fxaa", StringComparison.OrdinalIgnoreCase) ||
                       s.Equals("Final", StringComparison.OrdinalIgnoreCase) => FxaaExpander,
            _ => null,
        };

        if (target is null)
        {
            return;
        }

        Expander[] allSections =
        [
            EdgeDarkeningExpander,
            TurbulenceFlowExpander,
            ToneCurveExpander,
            PaperGranulationExpander,
            FxaaExpander,
        ];

        foreach (Expander expander in allSections)
        {
            expander.IsExpanded = ReferenceEquals(expander, target);
        }

        target.BringIntoView();
        Dispatcher.UIThread.Post(() => target.BringIntoView(), DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HistogramProperty && PART_ToneCurveEditor is not null)
        {
            PART_ToneCurveEditor.Histogram = Histogram;
        }
    }

    private void OnToneCurvePresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string presetName })
        {
            PART_ToneCurveEditor.ApplyPreset(presetName);
        }
    }

    private void OnResetToneCurveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FinishingStageViewModel vm)
        {
            vm.ResetToneCurveToBaseline();
            return;
        }

        PART_ToneCurveEditor.ResetToBaseline();
    }
}
