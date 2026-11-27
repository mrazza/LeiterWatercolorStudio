using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Leiter.Algorithms.Noise;
using LeiterWatercolorStudio.Core.Parameters;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Reusable collapsible card editor for <see cref="PerlinNoiseViewModel"/> supporting
/// live 2D fractal noise preview, seed randomization/reset, per-parameter sliders,
/// baseline reset, and drag coalescing events.
/// </summary>
public partial class PerlinNoiseEditor : UserControl
{
    private const double ModifiedEpsilon = 1e-6;
    private const int PreviewWidth = 56;
    private const int PreviewHeight = 36;
    private const double PreviewCoordScale = 3.0;

    /// <summary>
    /// Identifies the <see cref="Title"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string>(nameof(Title), "Noise");

    /// <summary>
    /// Identifies the <see cref="Description"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string?>(
            nameof(Description),
            "Fractal Brownian motion (fBm) Perlin noise parameters.");

    /// <summary>
    /// Identifies the <see cref="InfoText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> InfoTextProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string?>(
            nameof(InfoText),
            "Controls the procedural organic noise pattern used by this effect. Adjust Seed for a fresh layout, Frequency for pattern scale, and Octaves/Lacunarity/Persistence for layered roughness.");

    /// <summary>
    /// Identifies the <see cref="SeedInfoText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> SeedInfoTextProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string?>(
            nameof(SeedInfoText),
            "Shuffles the random pattern into a completely new organic layout without changing its strength or scale—like picking a fresh sheet of paper or a different brush motion. Use Random to explore variations.");

    /// <summary>
    /// Identifies the <see cref="FrequencyInfoText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> FrequencyInfoTextProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string?>(
            nameof(FrequencyInfoText),
            "Controls the size of the noise pattern. Lower values stretch the pattern into broad, sweeping waves; higher values pack it into tighter, finer ripples or grain.");

    /// <summary>
    /// Identifies the <see cref="OctavesInfoText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> OctavesInfoTextProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string?>(
            nameof(OctavesInfoText),
            "Sets how many layers of progressively finer detail are stacked together. 1 octave gives a smooth, simple wave; higher values add rougher, more natural fine texture with a small performance cost.");

    /// <summary>
    /// Identifies the <see cref="LacunarityInfoText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> LacunarityInfoTextProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string?>(
            nameof(LacunarityInfoText),
            "Controls how quickly detail shrinks from one layer to the next (visible when Octaves is above 1). Higher values jump rapidly from broad shapes to crisp fine grit; lower values keep layers closer in scale.");

    /// <summary>
    /// Identifies the <see cref="PersistenceInfoText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> PersistenceInfoTextProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string?>(
            nameof(PersistenceInfoText),
            "Controls how strongly the finer detail layers show up (visible when Octaves is above 1). Lower values keep the pattern soft and dominated by broad waves; higher values make fine-grained roughness much more noticeable.");

    /// <summary>
    /// Identifies the <see cref="IsExpanded"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, bool>(nameof(IsExpanded), true);

    /// <summary>
    /// Identifies the <see cref="DragStartedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragStartedCommandProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, ICommand?>(nameof(DragStartedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragCompletedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragCompletedCommandProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, ICommand?>(nameof(DragCompletedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragStarted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragStartedEvent =
        RoutedEvent.Register<PerlinNoiseEditor, RoutedEventArgs>(nameof(DragStarted), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="DragCompleted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragCompletedEvent =
        RoutedEvent.Register<PerlinNoiseEditor, RoutedEventArgs>(nameof(DragCompleted), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="SeedValidationError"/> direct property.
    /// </summary>
    public static readonly DirectProperty<PerlinNoiseEditor, string?> SeedValidationErrorProperty =
        AvaloniaProperty.RegisterDirect<PerlinNoiseEditor, string?>(
            nameof(SeedValidationError),
            o => o.SeedValidationError);

    private readonly ValidatedTextBoxHandler _seedInputHandler;
    private PerlinNoiseViewModel? _subscribedViewModel;
    private WriteableBitmap? _previewBitmap;
    private (int Seed, double Frequency, int Octaves, double Lacunarity, double Persistence)? _lastPreviewParams;
    private string? _seedValidationError;

    /// <summary>
    /// Initializes a new instance of <see cref="PerlinNoiseEditor"/>.
    /// </summary>
    public PerlinNoiseEditor()
    {
        InitializeComponent();

        _seedInputHandler = new ValidatedTextBoxHandler(
            PART_SeedTextBox,
            PART_SeedErrorText,
            () => DataContext is PerlinNoiseViewModel vm
                ? vm.Seed.ToString(CultureInfo.CurrentCulture)
                : string.Empty,
            rawText =>
            {
                if (DataContext is not PerlinNoiseViewModel vm)
                {
                    return null;
                }

                if (!ValidationFeedbackHelper.TryParseInt32(
                        rawText,
                        int.MinValue,
                        int.MaxValue,
                        out int seed,
                        out string error))
                {
                    return error;
                }

                if (vm.Seed != seed)
                {
                    vm.Seed = seed;
                }

                return null;
            },
            err => SeedValidationError = err);

        PART_ExpandButton.Click += OnExpandButtonClick;
        PART_ResetButton.Click += OnResetToBaselineClick;
        PART_RandomizeSeedButton.Click += OnRandomizeSeedClick;
        PART_ResetSeedButton.Click += OnResetSeedClick;

        AddHandler(NumericSlider.DragStartedEvent, OnChildSliderDragStarted);
        AddHandler(NumericSlider.DragCompletedEvent, OnChildSliderDragCompleted);

        SyncHeaderState();
        SyncExpansionState();
    }

    /// <summary>
    /// Occurs when an embedded slider drag begins.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    /// <summary>
    /// Occurs when an embedded slider drag completes.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    /// <summary>
    /// Gets or sets the header title displayed on the noise card.
    /// </summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the technical tooltip shown on the noise card title.
    /// </summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Gets or sets the layman-digestible explanation shown on the noise card's info icon.
    /// </summary>
    public string? InfoText
    {
        get => GetValue(InfoTextProperty);
        set => SetValue(InfoTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the layman explanation for the Seed control.
    /// </summary>
    public string? SeedInfoText
    {
        get => GetValue(SeedInfoTextProperty);
        set => SetValue(SeedInfoTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the layman explanation for the Frequency slider.
    /// </summary>
    public string? FrequencyInfoText
    {
        get => GetValue(FrequencyInfoTextProperty);
        set => SetValue(FrequencyInfoTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the layman explanation for the Octaves slider.
    /// </summary>
    public string? OctavesInfoText
    {
        get => GetValue(OctavesInfoTextProperty);
        set => SetValue(OctavesInfoTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the layman explanation for the Lacunarity slider.
    /// </summary>
    public string? LacunarityInfoText
    {
        get => GetValue(LacunarityInfoTextProperty);
        set => SetValue(LacunarityInfoTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the layman explanation for the Persistence slider.
    /// </summary>
    public string? PersistenceInfoText
    {
        get => GetValue(PersistenceInfoTextProperty);
        set => SetValue(PersistenceInfoTextProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the noise editor body is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when an embedded slider drag starts.
    /// </summary>
    public ICommand? DragStartedCommand
    {
        get => GetValue(DragStartedCommandProperty);
        set => SetValue(DragStartedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when an embedded slider drag completes.
    /// </summary>
    public ICommand? DragCompletedCommand
    {
        get => GetValue(DragCompletedCommandProperty);
        set => SetValue(DragCompletedCommandProperty, value);
    }

    /// <summary>
    /// Gets the active validation error message for the seed input, or <see langword="null"/> when valid.
    /// </summary>
    public string? SeedValidationError
    {
        get => _seedValidationError;
        private set => SetAndRaise(SeedValidationErrorProperty, ref _seedValidationError, value);
    }

    /// <summary>
    /// Gets the live 2D grayscale fractal noise preview bitmap rendered in the card header.
    /// </summary>
    public WriteableBitmap? NoisePreviewBitmap => _previewBitmap;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty ||
            change.Property == DescriptionProperty ||
            change.Property == InfoTextProperty ||
            change.Property == SeedInfoTextProperty ||
            change.Property == FrequencyInfoTextProperty ||
            change.Property == OctavesInfoTextProperty ||
            change.Property == LacunarityInfoTextProperty ||
            change.Property == PersistenceInfoTextProperty)
        {
            SyncHeaderState();
        }
        else if (change.Property == IsExpandedProperty)
        {
            SyncExpansionState();
        }
        else if (change.Property == DataContextProperty)
        {
            UpdateViewModelSubscription(change.NewValue as PerlinNoiseViewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateViewModelSubscription(DataContext as PerlinNoiseViewModel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateViewModelSubscription(null);
        DisposePreviewBitmap();
    }

    private void UpdateViewModelSubscription(PerlinNoiseViewModel? next)
    {
        if (ReferenceEquals(_subscribedViewModel, next))
        {
            SyncHeaderState();
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

        _seedInputHandler.ResetValidationAndSync();
        SyncHeaderState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            string.Equals(e.PropertyName, nameof(PerlinNoiseViewModel.Seed), StringComparison.Ordinal))
        {
            _seedInputHandler.ResetValidationAndSync();
        }

        SyncHeaderState();
    }

    private void SyncHeaderState()
    {
        if (PART_TitleText is null)
        {
            return;
        }

        _seedInputHandler.SyncFromModel();

        string effectiveTitle = string.IsNullOrWhiteSpace(Title) ? "Noise" : Title;
        PART_TitleText.Text = effectiveTitle;

        string? technicalTip = string.IsNullOrWhiteSpace(Description) ? null : Description;
        ToolTip.SetTip(PART_ExpandButton, technicalTip);

        bool hasTitleInfo = !string.IsNullOrWhiteSpace(InfoText);
        PART_TitleInfoIcon.Header = effectiveTitle;
        PART_TitleInfoIcon.Text = hasTitleInfo ? InfoText : null;
        PART_TitleInfoIcon.IsVisible = hasTitleInfo;

        PART_SeedInfoIcon.Text = SeedInfoText;
        PART_FrequencySlider.InfoText = FrequencyInfoText;
        PART_OctavesSlider.InfoText = OctavesInfoText;
        PART_LacunaritySlider.InfoText = LacunarityInfoText;
        PART_PersistenceSlider.InfoText = PersistenceInfoText;

        PerlinNoiseViewModel? vm = DataContext as PerlinNoiseViewModel;
        bool isModified = IsNoiseModified(vm);
        PART_ModifiedDot.IsVisible = isModified;
        PART_ResetButton.IsEnabled = vm is not null && isModified;
        PART_ResetButton.Opacity = isModified ? 1.0 : 0.3;

        bool isSeedModified = vm is not null && vm.IsSeedModified;
        PART_ResetSeedButton.IsEnabled = isSeedModified;
        PART_ResetSeedButton.Opacity = isSeedModified ? 1.0 : 0.25;

        UpdateNoisePreview(vm);
    }

    private void UpdateNoisePreview(PerlinNoiseViewModel? vm)
    {
        if (PART_PreviewImage is null)
        {
            return;
        }

        if (vm is null)
        {
            _lastPreviewParams = null;
            DisposePreviewBitmap();
            return;
        }

        double freq = double.IsFinite(vm.Frequency) && vm.Frequency > 0.0 ? vm.Frequency : 0.05;
        int octaves = Math.Clamp(vm.Octaves, 1, 16);
        double lacunarity = double.IsFinite(vm.Lacunarity) && vm.Lacunarity > 0.0 ? vm.Lacunarity : 2.0;
        double persistence = double.IsFinite(vm.Persistence) && vm.Persistence >= 0.0 ? vm.Persistence : 0.5;

        var key = (vm.Seed, freq, octaves, lacunarity, persistence);
        if (_lastPreviewParams == key && _previewBitmap is not null)
        {
            return;
        }

        _lastPreviewParams = key;

        var nextBitmap = new WriteableBitmap(
            new PixelSize(PreviewWidth, PreviewHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using (ILockedFramebuffer fb = nextBitmap.Lock())
        {
            var noise = new PerlinNoise(vm.Seed);
            double step = PreviewCoordScale * freq;
            unsafe
            {
                byte* basePtr = (byte*)fb.Address;
                for (int y = 0; y < PreviewHeight; y++)
                {
                    byte* row = basePtr + (y * fb.RowBytes);
                    double sy = y * step;
                    for (int x = 0; x < PreviewWidth; x++)
                    {
                        double sample = noise.SampleFractal(x * step, sy, octaves, lacunarity, persistence);
                        byte v = (byte)Math.Clamp((int)Math.Round(((sample * 0.65) + 0.5) * 255.0), 0, 255);
                        int offset = x * 4;
                        row[offset] = v;
                        row[offset + 1] = v;
                        row[offset + 2] = v;
                        row[offset + 3] = 255;
                    }
                }
            }
        }

        WriteableBitmap? oldBitmap = _previewBitmap;
        _previewBitmap = nextBitmap;
        PART_PreviewImage.Source = nextBitmap;
        oldBitmap?.Dispose();
    }

    private void DisposePreviewBitmap()
    {
        if (PART_PreviewImage is not null)
        {
            PART_PreviewImage.Source = null;
        }

        _previewBitmap?.Dispose();
        _previewBitmap = null;
        _lastPreviewParams = null;
    }

    private void SyncExpansionState()
    {
        if (PART_BodyPanel is null)
        {
            return;
        }

        bool expanded = IsExpanded;
        PART_BodyPanel.IsVisible = expanded;
        PART_ChevronText.Text = expanded ? "▾" : "▸";
    }

    private void OnExpandButtonClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsExpandedProperty, !IsExpanded);
    }

    private void OnRandomizeSeedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PerlinNoiseViewModel vm)
        {
            vm.RandomizeSeed();
            _seedInputHandler.ResetValidationAndSync();
        }
    }

    private void OnResetSeedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PerlinNoiseViewModel vm)
        {
            vm.ResetSeed();
            _seedInputHandler.ResetValidationAndSync();
        }
    }

    private void OnResetToBaselineClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PerlinNoiseViewModel vm)
        {
            vm.ResetToBaseline();
            _seedInputHandler.ResetValidationAndSync();
        }
    }

    private void OnChildSliderDragStarted(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(DragStartedEvent, this));
        ICommand? command = DragStartedCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnChildSliderDragCompleted(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(DragCompletedEvent, this));
        ICommand? command = DragCompletedCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private static bool IsNoiseModified(PerlinNoiseViewModel? vm)
    {
        if (vm is null)
        {
            return false;
        }

        PerlinNoiseOptions baseline = vm.Baseline;
        return vm.Seed != baseline.Seed ||
               Math.Abs(vm.Frequency - baseline.Frequency) > ModifiedEpsilon ||
               vm.Octaves != baseline.Octaves ||
               Math.Abs(vm.Lacunarity - baseline.Lacunarity) > ModifiedEpsilon ||
               Math.Abs(vm.Persistence - baseline.Persistence) > ModifiedEpsilon;
    }
}
