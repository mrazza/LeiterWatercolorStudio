using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Inspector panel for Stage 0 (Input Image) displaying source metadata, active baseline summary, and quick actions.
/// </summary>
public partial class InputInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="SourcePathText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> SourcePathTextProperty =
        AvaloniaProperty.Register<InputInspectorView, string?>(nameof(SourcePathText), "No image loaded");

    /// <summary>
    /// Identifies the <see cref="NativeDimensionsText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> NativeDimensionsTextProperty =
        AvaloniaProperty.Register<InputInspectorView, string?>(nameof(NativeDimensionsText), "—");

    /// <summary>
    /// Identifies the <see cref="PreviewDimensionsText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> PreviewDimensionsTextProperty =
        AvaloniaProperty.Register<InputInspectorView, string?>(nameof(PreviewDimensionsText), "—");

    /// <summary>
    /// Identifies the <see cref="BaselineSummaryText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> BaselineSummaryTextProperty =
        AvaloniaProperty.Register<InputInspectorView, string?>(nameof(BaselineSummaryText), "Default");

    /// <summary>
    /// Identifies the <see cref="PaletteSummaryText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> PaletteSummaryTextProperty =
        AvaloniaProperty.Register<InputInspectorView, string?>(nameof(PaletteSummaryText), "Auto");

    /// <summary>
    /// Identifies the <see cref="HasImage"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> HasImageProperty =
        AvaloniaProperty.Register<InputInspectorView, bool>(nameof(HasImage), false);

    /// <summary>
    /// Identifies the <see cref="OpenImageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> OpenImageCommandProperty =
        AvaloniaProperty.Register<InputInspectorView, ICommand?>(nameof(OpenImageCommand), null);

    /// <summary>
    /// Identifies the <see cref="RenderFullResCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> RenderFullResCommandProperty =
        AvaloniaProperty.Register<InputInspectorView, ICommand?>(nameof(RenderFullResCommand), null);

    /// <summary>
    /// Identifies the <see cref="ExportFinalCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ExportFinalCommandProperty =
        AvaloniaProperty.Register<InputInspectorView, ICommand?>(nameof(ExportFinalCommand), null);

    /// <summary>
    /// Initializes a new instance of <see cref="InputInspectorView"/>.
    /// </summary>
    public InputInspectorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the display text for the loaded source image path.
    /// </summary>
    public string? SourcePathText
    {
        get => GetValue(SourcePathTextProperty);
        set => SetValue(SourcePathTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the formatted native image dimensions readout.
    /// </summary>
    public string? NativeDimensionsText
    {
        get => GetValue(NativeDimensionsTextProperty);
        set => SetValue(NativeDimensionsTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the formatted preview working dimensions readout.
    /// </summary>
    public string? PreviewDimensionsText
    {
        get => GetValue(PreviewDimensionsTextProperty);
        set => SetValue(PreviewDimensionsTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the active baseline configuration summary text.
    /// </summary>
    public string? BaselineSummaryText
    {
        get => GetValue(BaselineSummaryTextProperty);
        set => SetValue(BaselineSummaryTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the active target palette summary text.
    /// </summary>
    public string? PaletteSummaryText
    {
        get => GetValue(PaletteSummaryTextProperty);
        set => SetValue(PaletteSummaryTextProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a source image is currently loaded.
    /// </summary>
    public bool HasImage
    {
        get => GetValue(HasImageProperty);
        set => SetValue(HasImageProperty, value);
    }

    /// <summary>
    /// Gets or sets the command executed when opening a source image.
    /// </summary>
    public ICommand? OpenImageCommand
    {
        get => GetValue(OpenImageCommandProperty);
        set => SetValue(OpenImageCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command executed to render the pipeline at full native resolution.
    /// </summary>
    public ICommand? RenderFullResCommand
    {
        get => GetValue(RenderFullResCommandProperty);
        set => SetValue(RenderFullResCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command executed to export the final watercolor image.
    /// </summary>
    public ICommand? ExportFinalCommand
    {
        get => GetValue(ExportFinalCommandProperty);
        set => SetValue(ExportFinalCommandProperty, value);
    }
}
