using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Leiter.Algorithms.Color;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using LeiterWatercolorStudio.App.Converters;
using LeiterWatercolorStudio.Core.Color;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Parameters.Stages;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Inspector panel for Stage 1 (Color Transfer), supporting Auto, Built-In, and Custom CIELAB palette controls.
/// </summary>
public partial class ColorTransferInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<ColorTransferInspectorView, ICommand?>(nameof(ResetStageCommand), null);

    /// <summary>
    /// Identifies the <see cref="ExtractPaletteCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ExtractPaletteCommandProperty =
        AvaloniaProperty.Register<ColorTransferInspectorView, ICommand?>(nameof(ExtractPaletteCommand), null);

    /// <summary>
    /// Gets all available <see cref="PaletteMode"/> options for UI selectors.
    /// </summary>
    public static IReadOnlyList<PaletteMode> PaletteModes { get; } = Enum.GetValues<PaletteMode>();

    /// <summary>
    /// Gets all supported CIE <see cref="Illuminant"/> values for custom palette editing.
    /// </summary>
    public static IReadOnlyList<Illuminant> Illuminants { get; } = Enum.GetValues<Illuminant>();

    private readonly Border[] _swatchBorders;
    private ColorTransferStageViewModel? _subscribedViewModel;

    /// <summary>
    /// Initializes a new instance of <see cref="ColorTransferInspectorView"/>.
    /// </summary>
    public ColorTransferInspectorView()
    {
        InitializeComponent();

        _swatchBorders =
        [
            PART_Swatch0,
            PART_Swatch1,
            PART_Swatch2,
            PART_Swatch3,
            PART_Swatch4,
        ];

        PART_EditAsCustomCopyButton.Click += OnEditAsCustomCopyClick;
        UpdatePalettePreview();
    }

    /// <summary>
    /// Gets or sets the command invoked when resetting Stage 1 to the active baseline.
    /// </summary>
    public ICommand? ResetStageCommand
    {
        get => GetValue(ResetStageCommandProperty);
        set => SetValue(ResetStageCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when extracting a palette from a reference image.
    /// </summary>
    public ICommand? ExtractPaletteCommand
    {
        get => GetValue(ExtractPaletteCommandProperty);
        set => SetValue(ExtractPaletteCommandProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
        {
            UpdateViewModelSubscription(change.NewValue as ColorTransferStageViewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateViewModelSubscription(DataContext as ColorTransferStageViewModel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateViewModelSubscription(null);
    }

    private void UpdateViewModelSubscription(ColorTransferStageViewModel? next)
    {
        if (ReferenceEquals(_subscribedViewModel, next))
        {
            UpdatePalettePreview();
            return;
        }

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = next;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdatePalettePreview();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null ||
            e.PropertyName == nameof(ColorTransferStageViewModel.PaletteMode) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.ActiveMatchedPalette) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.SelectedBuiltInPalette) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.SelectedBuiltInPaletteIndex) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.CustomPaletteName) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.MeanL) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.MeanA) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.MeanB) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.StdDevL) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.StdDevA) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.StdDevB) ||
            e.PropertyName == nameof(ColorTransferStageViewModel.CustomIlluminant))
        {
            UpdatePalettePreview();
        }
    }

    private void OnEditAsCustomCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ColorTransferStageViewModel vm)
        {
            return;
        }

        if (vm.PaletteMode == PaletteMode.BuiltIn &&
            !Equals(vm.ActiveMatchedPalette, vm.SelectedBuiltInPalette))
        {
            vm.SetCustomProfile(vm.SelectedBuiltInPalette);
            return;
        }

        vm.CopyMatchedToCustom();
    }

    private void UpdatePalettePreview()
    {
        if (PART_ActivePaletteNameText is null)
        {
            return;
        }

        if (DataContext is not ColorTransferStageViewModel vm)
        {
            PART_ActivePaletteBadgeText.Text = "Auto";
            PART_ActivePaletteNameText.Text = "No stage model";
            PART_ActivePaletteMetaText.Text = string.Empty;
            PART_ActiveSwatchStrip.IsVisible = false;
            PART_EditAsCustomCopyButton.IsEnabled = false;
            return;
        }

        PART_ActivePaletteBadgeText.Text = vm.PaletteMode switch
        {
            PaletteMode.Auto => vm.ActiveMatchedPalette is not null ? "Auto • Matched" : "Auto",
            PaletteMode.BuiltIn => "Built-In",
            PaletteMode.Custom => "Custom",
            _ => "Auto",
        };

        PART_EditAsCustomCopyButton.IsEnabled =
            vm.PaletteMode == PaletteMode.BuiltIn || vm.ActiveMatchedPalette is not null;

        ColorTransferProfile? previewProfile = ResolvePreviewProfile(vm);
        if (previewProfile is null)
        {
            PART_ActivePaletteNameText.Text = vm.PaletteMode == PaletteMode.Auto
                ? "Auto (pending render)"
                : "Invalid custom statistics";
            PART_ActivePaletteMetaText.Text = vm.PaletteMode == PaletteMode.Auto
                ? "Closest watercolor palette is selected automatically after Stage 1 runs."
                : "Adjust CIELAB parameters to valid finite values.";
            PART_ActiveSwatchStrip.IsVisible = false;
            return;
        }

        PART_ActivePaletteNameText.Text = previewProfile.Name;
        LabColorStatistics stats = previewProfile.Statistics;
        PART_ActivePaletteMetaText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"L* {stats.MeanL:F1} (±{stats.StdDevL:F1}) • a* {stats.MeanA:F1} (±{stats.StdDevA:F1}) • b* {stats.MeanB:F1} (±{stats.StdDevB:F1}) • {previewProfile.Illuminant}");

        Rgb8[] swatches = PaletteExtractor.SynthesizeSwatches(previewProfile, _swatchBorders.Length);
        for (int i = 0; i < _swatchBorders.Length; i++)
        {
            Rgb8 swatch = swatches[i];
            Color color = Rgb8ToBrushConverter.ToAvaloniaColor(swatch);
            _swatchBorders[i].Background = new SolidColorBrush(color);
            ToolTip.SetTip(_swatchBorders[i], $"#{swatch.R:X2}{swatch.G:X2}{swatch.B:X2}");
        }

        PART_ActiveSwatchStrip.IsVisible = true;
    }

    private static ColorTransferProfile? ResolvePreviewProfile(ColorTransferStageViewModel vm)
    {
        return vm.PaletteMode switch
        {
            PaletteMode.Auto => vm.ActiveMatchedPalette,
            PaletteMode.BuiltIn => vm.SelectedBuiltInPalette,
            PaletteMode.Custom => TryBuildCustomProfile(vm),
            _ => null,
        };
    }

    private static ColorTransferProfile? TryBuildCustomProfile(ColorTransferStageViewModel vm)
    {
        try
        {
            return vm.ToTargetPalette();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>
/// Converts a <see cref="PaletteMode"/> value into a boolean matching a configured target mode.
/// </summary>
public sealed class PaletteModeMatchConverter : IValueConverter
{
    /// <summary>
    /// Matches <see cref="PaletteMode.BuiltIn"/>.
    /// </summary>
    public static PaletteModeMatchConverter BuiltIn { get; } = new(PaletteMode.BuiltIn, invert: false);

    /// <summary>
    /// Matches <see cref="PaletteMode.Custom"/>.
    /// </summary>
    public static PaletteModeMatchConverter Custom { get; } = new(PaletteMode.Custom, invert: false);

    /// <summary>
    /// Matches any mode other than <see cref="PaletteMode.Custom"/>.
    /// </summary>
    public static PaletteModeMatchConverter NotCustom { get; } = new(PaletteMode.Custom, invert: true);

    private readonly PaletteMode _target;
    private readonly bool _invert;

    private PaletteModeMatchConverter(PaletteMode target, bool invert)
    {
        _target = target;
        _invert = invert;
    }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PaletteMode mode)
        {
            bool match = mode == _target;
            return _invert ? !match : match;
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

/// <summary>
/// Converts a <see cref="ColorTransferProfile"/> into five representative sRGB swatches via <see cref="PaletteExtractor.SynthesizeSwatches"/>.
/// </summary>
public sealed class ProfileToSwatchesConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="ProfileToSwatchesConverter"/>.
    /// </summary>
    public static ProfileToSwatchesConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ColorTransferProfile profile)
        {
            return PaletteExtractor.SynthesizeSwatches(profile, 5);
        }

        return Array.Empty<Rgb8>();
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
