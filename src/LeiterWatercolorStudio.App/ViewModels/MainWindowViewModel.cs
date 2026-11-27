using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.App.Rendering;
using LeiterWatercolorStudio.App.Services;
using LeiterWatercolorStudio.Core.Color;
using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Parameters;
using LeiterWatercolorStudio.Core.Pipeline;
using LeiterWatercolorStudio.Core.Pipeline.Events;
using LeiterWatercolorStudio.Core.Presets;
using LeiterSize = Leiter.Core.Size;

namespace LeiterWatercolorStudio.App.ViewModels;

/// <summary>
/// Primary view model orchestrating image loading, parameter editing, debounced pipeline execution,
/// A/B comparison, stage/sub-stage inspection, preset I/O, and full-resolution exports.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IFileDialogService _fileDialogs;
    private readonly Action<Action> _uiDispatcher;
    private readonly Dictionary<StageViewId, VisualizationMode> _viewModes = new();

    private StageCardViewModel? _selectedStage;
    private SubStageItemViewModel? _selectedSubStage;
    private bool _showSubStageDrawer;
    private IReadOnlyList<VisualizationMode> _availableVisualizationModes = Array.Empty<VisualizationMode>();
    private VisualizationMode _selectedVisualizationMode = VisualizationMode.Rgb;
    private bool _showReseedButton;

    private WriteableBitmap? _primaryBitmap;
    private WriteableBitmap? _compareBitmap;
    private string _primaryLabel = string.Empty;
    private string _compareLabel = string.Empty;
    private CompareMode _compareMode = CompareMode.Off;
    private CompareMode _lastNonOffCompareMode = CompareMode.SplitWipe;
    private CompareReferenceKind _compareReferenceKind = CompareReferenceKind.Input;
    private PrimaryStageId? _pinnedStageId;
    private bool _isHoldComparing;
    private double _wipePosition = 0.5;
    private double _zoom = 1.0;
    private double _panX;
    private double _panY;
    private double _gridZoom = 1.0;
    private double _gridPanX;
    private double _gridPanY;

    private bool _isGridView;
    private bool _isDarkTheme = true;
    private bool _isInspectorOpen = true;
    private PreviewResolutionCap _previewCap = PreviewResolutionCap.Px1024;
    private BaselineMode _activeBaselineMode = BaselineMode.ForImage;
    private bool _isAutoRunEnabled = true;

    private bool _isBusy;
    private string _progressText = string.Empty;
    private string _statusText = "Ready — Open an image to begin.";
    private string? _hoverHudText;
    private string? _hoverHudDetailsText;
    private bool _showHoverDiagnostics;
    private Color? _hoverSwatchColor;
    private bool _hasImage;
    private string _sourceFileDisplayText = "No image loaded";
    private string _nativeResolutionText = "—";
    private string _previewResolutionText = "—";
    private string _baselineSummaryText = "For Image (Auto-analyzed)";
    private string _paletteSummaryText = "—";
    private string _segmentationStatsText = "Regions: — • Boundaries: —";
    private string _wetInWetStatsText = "Qualifying Boundaries: —";
    private string _stageTimingSummaryText = "—";
    private IReadOnlyList<double>? _toneCurveHistogram;
    private Bitmap? _referencePaletteThumbnail;
    private string? _referenceImageName;

    private IReadOnlyMatrix<Rgb8>? _currentDisplayedMatrix;
    private IReadOnlyMatrix<Rgb8>? _currentCompareMatrix;
    private CancellationTokenSource? _exportCts;
    private WatercolorResult? _cachedFullResResult;
    private WatercolorOptions? _cachedFullResOptions;
    private bool _isViewingFullRes;
    private int _regionRandomSeed = 42;
    private bool _isSyncingStageState;
    private bool _isSyncingVisualizationMode;
    private bool _isApplyingBaseline;
    private bool _isLoadingImage;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="MainWindowViewModel"/> using default Avalonia UI services.
    /// </summary>
    public MainWindowViewModel()
        : this(null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="MainWindowViewModel"/> with injectable dialog, dispatch, and debounce dependencies.
    /// </summary>
    public MainWindowViewModel(
        IFileDialogService? fileDialogs,
        Action<Action>? uiDispatcher = null,
        TimeSpan? debounceInterval = null)
    {
        _fileDialogs = fileDialogs ?? new AvaloniaFileDialogService();
        _uiDispatcher = uiDispatcher ?? (action => Dispatcher.UIThread.Post(action));

        Engine = new PipelineEngine(debounceInterval, _previewCap);
        Parameters = new PipelineParametersViewModel(WatercolorOptions.Default, _activeBaselineMode);

        Stages = new ObservableCollection<StageCardViewModel>();
        foreach (PipelineStageDescriptor descriptor in PipelineStageRegistry.Stages)
        {
            Stages.Add(new StageCardViewModel(descriptor));
        }

        foreach (StageViewDescriptor view in PipelineStageRegistry.Views)
        {
            _viewModes[view.ViewId] = view.DefaultMode;
        }

        SubStages = new ObservableCollection<SubStageItemViewModel>();

        Engine.RenderStarted += OnEngineRenderStarted;
        Engine.StepProgress += OnEngineStepProgress;
        Engine.RenderCompleted += OnEngineRenderCompleted;
        Engine.RenderCancelled += OnEngineRenderCancelled;
        Engine.RenderFailed += OnEngineRenderFailed;
        Engine.StalenessChanged += OnEngineStalenessChanged;

        Parameters.OptionsChanged += OnParametersOptionsChanged;
        Parameters.PropertyChanged += OnParametersPropertyChanged;
        Parameters.ColorTransfer.PropertyChanged += OnBypassableStagePropertyChanged;
        Parameters.LodAbstraction.PropertyChanged += OnBypassableStagePropertyChanged;
        Parameters.WetInWet.PropertyChanged += OnBypassableStagePropertyChanged;
        Parameters.HandTremor.PropertyChanged += OnBypassableStagePropertyChanged;

        SelectedStage = Stages[0];
        UpdateBaselineSummaryText();
    }

    /// <summary>
    /// Raised when an underlying <see cref="WriteableBitmap"/> buffer was mutated in place
    /// so visual hosts invalidate their surface.
    /// </summary>
    public event EventHandler? ViewportRefreshRequested;

    /// <summary>
    /// Raised after a new image has been loaded so the viewport can fit it comfortably in view.
    /// </summary>
    public event EventHandler? ImageLoaded;

    /// <summary>
    /// Raised when selecting a sub-stage pill or artist look shortcut should expand and scroll
    /// to a specific inspector section (e.g., <c>"EdgeDarkening"</c>, <c>"PaperGranulation"</c>).
    /// </summary>
    public event Action<string>? InspectorSectionFocusRequested;

    /// <summary>
    /// Gets the underlying pipeline execution engine.
    /// </summary>
    public PipelineEngine Engine { get; }

    /// <summary>
    /// Gets the editable parameter view model tree for Stages 1–6.
    /// </summary>
    public PipelineParametersViewModel Parameters { get; }

    /// <summary>
    /// Gets the 8 primary stage card view models displayed in the filmstrip and grid view.
    /// </summary>
    public ObservableCollection<StageCardViewModel> Stages { get; }

    /// <summary>
    /// Gets the sub-stage items belonging to <see cref="SelectedStage"/>.
    /// </summary>
    public ObservableCollection<SubStageItemViewModel> SubStages { get; }

    /// <summary>
    /// Gets the ordered preview resolution cap choices for toolbar selection.
    /// </summary>
    public IReadOnlyList<PreviewResolutionCap> PreviewCapOptions { get; } =
    [
        PreviewResolutionCap.Px512,
        PreviewResolutionCap.Px768,
        PreviewResolutionCap.Px1024,
        PreviewResolutionCap.Px1536,
        PreviewResolutionCap.Native,
    ];

    /// <summary>
    /// Gets the ordered baseline calibration modes for toolbar selection.
    /// </summary>
    public IReadOnlyList<BaselineMode> BaselineModeOptions { get; } =
    [
        BaselineMode.ForImage,
        BaselineMode.ForSize,
        BaselineMode.Default,
    ];

    /// <summary>
    /// Gets the ordered viewport comparison modes for toolbar selection.
    /// </summary>
    public IReadOnlyList<CompareMode> CompareModeOptions { get; } =
    [
        CompareMode.Off,
        CompareMode.SplitWipe,
        CompareMode.SideBySide,
    ];

    /// <summary>
    /// Gets the ordered A/B comparison reference sources for toolbar selection.
    /// </summary>
    public IReadOnlyList<CompareReferenceKind> CompareReferenceOptions { get; } =
    [
        CompareReferenceKind.Input,
        CompareReferenceKind.PreviousStage,
        CompareReferenceKind.PinnedStage,
    ];

    /// <summary>
    /// Gets or sets the currently selected primary stage card.
    /// </summary>
    public StageCardViewModel? SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (SetProperty(ref _selectedStage, value))
            {
                HandleSelectedStageChanged(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the currently selected sub-stage pill.
    /// </summary>
    public SubStageItemViewModel? SelectedSubStage
    {
        get => _selectedSubStage;
        set
        {
            if (SetProperty(ref _selectedSubStage, value))
            {
                HandleSelectedSubStageChanged(value);
            }
        }
    }

    /// <summary>
    /// Gets whether the collapsible sub-stage drawer should be displayed above the filmstrip.
    /// </summary>
    public bool ShowSubStageDrawer
    {
        get => _showSubStageDrawer;
        private set => SetProperty(ref _showSubStageDrawer, value);
    }

    /// <summary>
    /// Gets the display title of the active primary stage.
    /// </summary>
    public string ActiveStageTitle => _selectedStage?.Title ?? "Input";

    /// <summary>
    /// Gets the unified 1–8 header title for the pinned Stage Inspector header.
    /// </summary>
    public string ActiveStageHeaderTitle => ActiveStageId switch
    {
        PrimaryStageId.Input => "1. Input",
        PrimaryStageId.ColorTransfer => "2. Color Transfer",
        PrimaryStageId.Saliency => "3. Saliency",
        PrimaryStageId.Segmentation => "4. Segmentation",
        PrimaryStageId.LodAbstraction => "5. LOD Abstraction",
        PrimaryStageId.WetInWet => "6. Wet-in-Wet",
        PrimaryStageId.HandTremor => "7. Hand Tremor",
        PrimaryStageId.Finishing => "8. Finishing",
        _ => ActiveStageTitle,
    };

    /// <summary>
    /// Gets the concise subtitle for the pinned Stage Inspector header.
    /// </summary>
    public string ActiveStageSubtitle => ActiveStageId switch
    {
        PrimaryStageId.Input => "Source metadata, baseline & look shortcuts",
        PrimaryStageId.ColorTransfer => "CIELAB palette matching & color shift",
        PrimaryStageId.Saliency => "EGBI, regional contrast & SaliencyCut",
        PrimaryStageId.Segmentation => "Mean-shift filtering & region clustering",
        PrimaryStageId.LodAbstraction => "Saliency-guided brushstroke abstraction",
        PrimaryStageId.WetInWet => "Edge bleeding & capillary diffusion",
        PrimaryStageId.HandTremor => "Boundary perturbation & paper gaps",
        PrimaryStageId.Finishing => "Pigment washes, tone curve, grain & FXAA",
        _ => string.Empty,
    };

    /// <summary>
    /// Gets a quiet summary label for the sub-stage bar when the active stage has no sub-stages.
    /// </summary>
    public string ActiveStageSummary => ActiveStageId switch
    {
        PrimaryStageId.Input => HasImage
            ? "Single-view stage — source photo before watercolor processing"
            : "Open an image or try the built-in sample photo to begin",
        PrimaryStageId.ColorTransfer => "Single-view stage — Reinhard CIELAB palette transfer",
        PrimaryStageId.LodAbstraction => "Single-view stage — saliency-guided brushstroke abstraction",
        PrimaryStageId.HandTremor => "Single-view stage — boundary perturbation & paper gaps",
        _ => "Single-view stage — no sub-views",
    };

    /// <summary>
    /// Gets whether the active stage supports an enable/bypass toggle in the pinned inspector header.
    /// </summary>
    public bool ActiveStageHasBypass => ActiveStageId is
        PrimaryStageId.ColorTransfer or
        PrimaryStageId.LodAbstraction or
        PrimaryStageId.WetInWet or
        PrimaryStageId.HandTremor;

    /// <summary>
    /// Gets or sets whether the active bypassable stage is enabled.
    /// </summary>
    public bool ActiveStageIsEnabled
    {
        get => ActiveStageId switch
        {
            PrimaryStageId.ColorTransfer => Parameters.ColorTransfer.EnableColorTransfer,
            PrimaryStageId.LodAbstraction => Parameters.LodAbstraction.EnableLodAbstraction,
            PrimaryStageId.WetInWet => Parameters.WetInWet.EnableWetInWet,
            PrimaryStageId.HandTremor => Parameters.HandTremor.EnableHandTremor,
            _ => true,
        };
        set
        {
            switch (ActiveStageId)
            {
                case PrimaryStageId.ColorTransfer:
                    Parameters.ColorTransfer.EnableColorTransfer = value;
                    break;
                case PrimaryStageId.LodAbstraction:
                    Parameters.LodAbstraction.EnableLodAbstraction = value;
                    break;
                case PrimaryStageId.WetInWet:
                    Parameters.WetInWet.EnableWetInWet = value;
                    break;
                case PrimaryStageId.HandTremor:
                    Parameters.HandTremor.EnableHandTremor = value;
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the active <see cref="PrimaryStageId"/>.
    /// </summary>
    public PrimaryStageId ActiveStageId => _selectedStage?.StageId ?? PrimaryStageId.Input;

    /// <summary>
    /// Gets whether the active stage supports per-stage parameter reset.
    /// </summary>
    public bool CanResetActiveStage => ActiveStageId != PrimaryStageId.Input;

    /// <summary>
    /// Gets whether the active stage is currently pinned for A/B comparison.
    /// </summary>
    public bool IsCurrentStagePinned => _pinnedStageId.HasValue && _pinnedStageId.Value == ActiveStageId;

    /// <summary>
    /// Gets the visualization modes valid for the active stage view.
    /// </summary>
    public IReadOnlyList<VisualizationMode> AvailableVisualizationModes
    {
        get => _availableVisualizationModes;
        private set => SetProperty(ref _availableVisualizationModes, value);
    }

    /// <summary>
    /// Gets or sets the active visualization mode for the current stage view.
    /// </summary>
    public VisualizationMode SelectedVisualizationMode
    {
        get => _selectedVisualizationMode;
        set
        {
            if (SetProperty(ref _selectedVisualizationMode, value) && !_isSyncingVisualizationMode)
            {
                HandleVisualizationModeChanged(value);
            }
        }
    }

    /// <summary>
    /// Gets whether the viewport should show the random-region color reseed button.
    /// </summary>
    public bool ShowReseedButton
    {
        get => _showReseedButton;
        private set => SetProperty(ref _showReseedButton, value);
    }

    /// <summary>
    /// Gets the primary rendered bitmap displayed in the viewport.
    /// </summary>
    public WriteableBitmap? PrimaryBitmap
    {
        get => _primaryBitmap;
        private set => SetProperty(ref _primaryBitmap, value);
    }

    /// <summary>
    /// Gets the reference bitmap displayed in split-wipe or side-by-side comparison mode.
    /// </summary>
    public WriteableBitmap? CompareBitmap
    {
        get => _compareBitmap;
        private set => SetProperty(ref _compareBitmap, value);
    }

    /// <summary>
    /// Gets the overlay label for the primary viewport pane.
    /// </summary>
    public string PrimaryLabel
    {
        get => _primaryLabel;
        private set => SetProperty(ref _primaryLabel, value);
    }

    /// <summary>
    /// Gets the overlay label for the comparison reference pane.
    /// </summary>
    public string CompareLabel
    {
        get => _compareLabel;
        private set => SetProperty(ref _compareLabel, value);
    }

    /// <summary>
    /// Gets or sets the active viewport A/B comparison mode.
    /// </summary>
    public CompareMode CompareMode
    {
        get => _compareMode;
        set
        {
            if (SetProperty(ref _compareMode, value))
            {
                if (value != CompareMode.Off)
                {
                    _lastNonOffCompareMode = value;
                }

                RefreshCompareBitmapOnly();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether Hold-to-Compare is temporarily showing the reference image.
    /// </summary>
    public bool IsHoldComparing
    {
        get => _isHoldComparing;
        set
        {
            if (SetProperty(ref _isHoldComparing, value))
            {
                RefreshCompareBitmapOnly();
            }
        }
    }

    /// <summary>
    /// Gets or sets which reference image is displayed in comparison mode.
    /// </summary>
    public CompareReferenceKind CompareReferenceKind
    {
        get => _compareReferenceKind;
        set
        {
            if (SetProperty(ref _compareReferenceKind, value))
            {
                RefreshCompareBitmapOnly();
            }
        }
    }

    /// <summary>
    /// Gets or sets the stage pinned as an A/B comparison reference.
    /// </summary>
    public PrimaryStageId? PinnedStageId
    {
        get => _pinnedStageId;
        set
        {
            if (SetProperty(ref _pinnedStageId, value))
            {
                SyncPinnedCardStates();
                OnPropertyChanged(nameof(IsCurrentStagePinned));
                if (CompareReferenceKind == CompareReferenceKind.PinnedStage &&
                    (CompareMode != CompareMode.Off || IsHoldComparing))
                {
                    RefreshCompareBitmapOnly();
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the normalized split-wipe divider position in <c>[0, 1]</c>.
    /// </summary>
    public double WipePosition
    {
        get => _wipePosition;
        set => SetProperty(ref _wipePosition, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    /// Gets or sets the single-viewport zoom factor.
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, double.IsFinite(value) ? Math.Clamp(value, 0.1, 16.0) : 1.0);
    }

    /// <summary>
    /// Gets or sets the single-viewport horizontal pan offset.
    /// </summary>
    public double PanX
    {
        get => _panX;
        set => SetProperty(ref _panX, double.IsFinite(value) ? value : 0.0);
    }

    /// <summary>
    /// Gets or sets the single-viewport vertical pan offset.
    /// </summary>
    public double PanY
    {
        get => _panY;
        set => SetProperty(ref _panY, double.IsFinite(value) ? value : 0.0);
    }

    /// <summary>
    /// Gets or sets the independent 4×2 grid zoom factor (default <c>1.0</c> = Fit).
    /// </summary>
    public double GridZoom
    {
        get => _gridZoom;
        set => SetProperty(ref _gridZoom, double.IsFinite(value) ? Math.Clamp(value, 0.1, 16.0) : 1.0);
    }

    /// <summary>
    /// Gets or sets the independent 4×2 grid horizontal pan offset.
    /// </summary>
    public double GridPanX
    {
        get => _gridPanX;
        set => SetProperty(ref _gridPanX, double.IsFinite(value) ? value : 0.0);
    }

    /// <summary>
    /// Gets or sets the independent 4×2 grid vertical pan offset.
    /// </summary>
    public double GridPanY
    {
        get => _gridPanY;
        set => SetProperty(ref _gridPanY, double.IsFinite(value) ? value : 0.0);
    }

    /// <summary>
    /// Gets or sets whether the 4×2 multi-up stage grid view is active instead of the single viewport.
    /// </summary>
    public bool IsGridView
    {
        get => _isGridView;
        set => SetProperty(ref _isGridView, value);
    }

    /// <summary>
    /// Gets or sets whether the Fluent Dark studio theme is active (<see langword="false"/> for Light).
    /// </summary>
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetProperty(ref _isDarkTheme, value);
    }

    /// <summary>
    /// Gets or sets whether the right-hand parameter inspector sidebar is expanded.
    /// </summary>
    public bool IsInspectorOpen
    {
        get => _isInspectorOpen;
        set => SetProperty(ref _isInspectorOpen, value);
    }

    /// <summary>
    /// Gets or sets the interactive preview maximum dimension cap.
    /// </summary>
    public PreviewResolutionCap PreviewCap
    {
        get => _previewCap;
        set
        {
            if (SetProperty(ref _previewCap, value))
            {
                ApplyPreviewCapChange(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the active baseline calibration mode.
    /// </summary>
    public BaselineMode ActiveBaselineMode
    {
        get => _activeBaselineMode;
        set
        {
            if (SetProperty(ref _activeBaselineMode, value) && !_isApplyingBaseline)
            {
                ApplyBaselineSwitch(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether parameter edits automatically queue debounced preview renders.
    /// </summary>
    public bool IsAutoRunEnabled
    {
        get => _isAutoRunEnabled;
        set
        {
            if (SetProperty(ref _isAutoRunEnabled, value))
            {
                Engine.IsAutoRunEnabled = value;
                OnPropertyChanged(nameof(IsRunHighlighted));
                if (value && Engine.HasImage && Engine.StaleStages.Count > 0)
                {
                    _ = Engine.RunNowAsync();
                }
            }
        }
    }

    /// <summary>
    /// Gets whether any pipeline stage has unrendered parameter edits.
    /// </summary>
    public bool HasPendingChanges => Engine.HasImage && Engine.StaleStages.Count > 0;

    /// <summary>
    /// Gets whether the Run button should be highlighted with the primary accent style.
    /// </summary>
    public bool IsRunHighlighted => HasImage && (!IsAutoRunEnabled || HasPendingChanges);

    /// <summary>
    /// Gets whether a full-resolution render matching the current parameters is cached in memory.
    /// </summary>
    public bool HasCachedFullRes => _cachedFullResResult is not null;

    /// <summary>
    /// Gets or sets whether the viewport is currently displaying the cached full-resolution render.
    /// </summary>
    public bool IsViewingFullRes
    {
        get => _isViewingFullRes;
        set
        {
            bool target = value && HasCachedFullRes;
            if (SetProperty(ref _isViewingFullRes, target))
            {
                if (Engine.PreviewSize.Width > 0 &&
                    Engine.NativeSize.Width > 0 &&
                    Engine.PreviewSize.Width != Engine.NativeSize.Width)
                {
                    double ratio = target
                        ? (double)Engine.PreviewSize.Width / Engine.NativeSize.Width
                        : (double)Engine.NativeSize.Width / Engine.PreviewSize.Width;
                    Zoom *= ratio;
                }

                OnPropertyChanged(nameof(FullResToggleLabel));
                RefreshViewportDisplay();
                if (target && Engine.NativeSize.Width > 0)
                {
                    StatusText = $"Viewing cached full resolution ({Engine.NativeSize.Width}×{Engine.NativeSize.Height}).";
                }
                else if (HasImage)
                {
                    StatusText = $"Viewing interactive preview ({PreviewResolutionText}).";
                }
            }
        }
    }

    /// <summary>
    /// Gets the label for the full-resolution preview toggle badge.
    /// </summary>
    public string FullResToggleLabel => IsViewingFullRes ? "Full Res (cached)" : "Preview Full-Res";

    /// <summary>
    /// Gets the pre-tone-curve Rec.709 luminance histogram for Stage 8's tone curve backdrop.
    /// </summary>
    public IReadOnlyList<double>? ToneCurveHistogram
    {
        get => _toneCurveHistogram;
        private set => SetProperty(ref _toneCurveHistogram, value);
    }

    /// <summary>
    /// Gets the thumbnail bitmap of the last extracted Stage 2 reference image.
    /// </summary>
    public Bitmap? ReferencePaletteThumbnail
    {
        get => _referencePaletteThumbnail;
        private set => SetProperty(ref _referencePaletteThumbnail, value);
    }

    /// <summary>
    /// Gets the filename of the last extracted Stage 2 reference image.
    /// </summary>
    public string? ReferenceImageName
    {
        get => _referenceImageName;
        private set => SetProperty(ref _referenceImageName, value);
    }

    /// <summary>
    /// Gets whether a preview render, full-res render, or export task is currently executing.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyActionCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current active step or export progress readout.
    /// </summary>
    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    /// <summary>
    /// Gets the status bar message describing the most recent operation.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the compact 1-line hover pixel diagnostic HUD text.
    /// </summary>
    public string? HoverHudText
    {
        get => _hoverHudText;
        private set => SetProperty(ref _hoverHudText, value);
    }

    /// <summary>
    /// Gets the secondary multi-line stage diagnostics for the hovered pixel.
    /// </summary>
    public string? HoverHudDetailsText
    {
        get => _hoverHudDetailsText;
        private set => SetProperty(ref _hoverHudDetailsText, value);
    }

    /// <summary>
    /// Gets or sets whether detailed per-pixel stage diagnostics are expanded inside the hover HUD.
    /// </summary>
    public bool ShowHoverDiagnostics
    {
        get => _showHoverDiagnostics;
        set => SetProperty(ref _showHoverDiagnostics, value);
    }

    /// <summary>
    /// Gets the RGB color swatch under the hovered pixel.
    /// </summary>
    public Color? HoverSwatchColor
    {
        get => _hoverSwatchColor;
        private set => SetProperty(ref _hoverSwatchColor, value);
    }

    /// <summary>
    /// Gets whether an image is currently loaded in the studio.
    /// </summary>
    public bool HasImage
    {
        get => _hasImage;
        private set
        {
            if (SetProperty(ref _hasImage, value))
            {
                OnPropertyChanged(nameof(ActiveStageSummary));
                OnPropertyChanged(nameof(IsRunHighlighted));
                NotifyActionCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the display filename or path summary of the loaded source image.
    /// </summary>
    public string SourceFileDisplayText
    {
        get => _sourceFileDisplayText;
        private set => SetProperty(ref _sourceFileDisplayText, value);
    }

    /// <summary>
    /// Gets the formatted native image dimensions (e.g., <c>"1920 × 1080"</c>).
    /// </summary>
    public string NativeResolutionText
    {
        get => _nativeResolutionText;
        private set => SetProperty(ref _nativeResolutionText, value);
    }

    /// <summary>
    /// Gets the formatted preview image dimensions (e.g., <c>"1024 × 576"</c>).
    /// </summary>
    public string PreviewResolutionText
    {
        get => _previewResolutionText;
        private set => SetProperty(ref _previewResolutionText, value);
    }

    /// <summary>
    /// Gets a concise description of the active baseline calibration mode.
    /// </summary>
    public string BaselineSummaryText
    {
        get => _baselineSummaryText;
        private set => SetProperty(ref _baselineSummaryText, value);
    }

    /// <summary>
    /// Gets a concise summary of the active or matched Stage 1 palette.
    /// </summary>
    public string PaletteSummaryText
    {
        get => _paletteSummaryText;
        private set => SetProperty(ref _paletteSummaryText, value);
    }

    /// <summary>
    /// Gets live segmentation region and boundary counts from the last render.
    /// </summary>
    public string SegmentationStatsText
    {
        get => _segmentationStatsText;
        private set => SetProperty(ref _segmentationStatsText, value);
    }

    /// <summary>
    /// Gets live qualifying wet-in-wet boundary statistics from the last render.
    /// </summary>
    public string WetInWetStatsText
    {
        get => _wetInWetStatsText;
        private set => SetProperty(ref _wetInWetStatsText, value);
    }

    /// <summary>
    /// Gets a compact cache and timing summary for the bottom status bar.
    /// </summary>
    public string StageTimingSummaryText
    {
        get => _stageTimingSummaryText;
        private set => SetProperty(ref _stageTimingSummaryText, value);
    }

    /// <summary>
    /// Loads a source image from disk, computes image analysis, calibrates the active baseline,
    /// and initiates a preview render.
    /// </summary>
    public async Task LoadImageFromPathAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            StatusText = $"Loading {Path.GetFileName(path)}...";
            SequentialMatrix<Rgb8> matrix = await Task.Run(() => ImageExtensions.LoadRgb8(path));
            if (_disposed)
            {
                return;
            }

            LoadLoadedImage(matrix, path, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusText = $"Failed to open image: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("Failed to Open Image", $"Could not open '{path}':\n{ex.Message}");
        }
    }

    /// <summary>
    /// Loads an in-memory source matrix into the studio and initiates a preview render.
    /// </summary>
    public void LoadImageFromMatrix(SequentialMatrix<Rgb8> matrix, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LoadLoadedImage(matrix, sourcePath: null, displayName ?? "In-Memory Image");
    }

    /// <summary>
    /// Samples full diagnostic pixel readouts at (<paramref name="x"/>, <paramref name="y"/>) and updates the hover HUD.
    /// </summary>
    public void UpdateHoverPixel(int x, int y, bool isCompareSide = false)
    {
        bool useFullRes = IsViewingFullRes && _cachedFullResResult is not null && Engine.NativeSource is not null;
        IReadOnlyMatrix<Rgb8>? sourceMatrix = useFullRes ? Engine.NativeSource : Engine.PreviewSource;
        WatercolorResult? activeResult = useFullRes ? _cachedFullResResult : Engine.LastResult;

        if (!Engine.HasImage || sourceMatrix is null)
        {
            ClearHoverPixel();
            return;
        }

        StageViewId activeView = GetActiveStageViewId();
        StageViewId sampledView = activeView;
        IReadOnlyMatrix<Rgb8>? displayedMatrix = _currentDisplayedMatrix;

        if (isCompareSide && (CompareMode != CompareMode.Off || IsHoldComparing))
        {
            sampledView = ResolveCompareStageViewId(activeView);
            displayedMatrix = CompareReferenceKind == CompareReferenceKind.Input
                ? sourceMatrix
                : _currentCompareMatrix ?? sourceMatrix;
        }

        if (!PixelInspectorReadout.TrySample(
                x,
                y,
                sampledView,
                sourceMatrix,
                activeResult,
                out PixelInspectorReadout readout,
                displayedMatrix))
        {
            ClearHoverPixel();
            return;
        }

        string fullHud = readout.FormatHudText();
        int newlineIndex = fullHud.IndexOf('\n');
        string primaryLine = newlineIndex >= 0 ? fullHud[..newlineIndex] : fullHud;
        HoverHudText = isCompareSide ? $"[Ref] {primaryLine}" : primaryLine;
        HoverHudDetailsText = newlineIndex >= 0 && newlineIndex + 1 < fullHud.Length
            ? fullHud[(newlineIndex + 1)..]
            : null;
        HoverSwatchColor = Color.FromRgb(readout.Rgb.R, readout.Rgb.G, readout.Rgb.B);
    }

    /// <summary>
    /// Clears the hover pixel diagnostic readout.
    /// </summary>
    public void ClearHoverPixel()
    {
        HoverHudText = null;
        HoverHudDetailsText = null;
        HoverSwatchColor = null;
    }

    /// <summary>
    /// Begins a coalesced parameter drag transaction on the undo stack.
    /// </summary>
    public void BeginParameterDrag() => Parameters.BeginDrag();

    /// <summary>
    /// Commits the active parameter drag transaction onto the undo stack.
    /// </summary>
    public void EndParameterDrag() => Parameters.EndDrag();

    [RelayCommand]
    private async Task OpenImageAsync()
    {
        string? path = await _fileDialogs.PickOpenImageAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await LoadImageFromPathAsync(path);
        }
    }

    [RelayCommand]
    private void LoadSampleImage()
    {
        SequentialMatrix<Rgb8> sample = CreateSampleLandscapeMatrix(768, 512);
        LoadImageFromMatrix(sample, "sample_landscape.png");
    }

    [RelayCommand]
    private async Task ExtractReferencePaletteAsync()
    {
        string? path = await _fileDialogs.PickOpenImageAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            StatusText = $"Extracting palette from {Path.GetFileName(path)}...";
            (ColorTransferProfile profile, SequentialMatrix<Rgb8> refMatrix) = await Task.Run(() =>
            {
                SequentialMatrix<Rgb8> loaded = ImageExtensions.LoadRgb8(path);
                string profileName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    profileName = "Custom Palette";
                }

                ColorTransferProfile extracted = PaletteExtractor.ExtractFromMatrix(loaded, profileName);
                return (extracted, loaded);
            });
            if (_disposed)
            {
                return;
            }

            WriteableBitmap nextThumb = MatrixBitmapConverter.BlitThumbnail(refMatrix, maxEdge: 120);
            Bitmap? oldThumb = _referencePaletteThumbnail;
            ReferencePaletteThumbnail = nextThumb;
            ReferenceImageName = Path.GetFileName(path);
            oldThumb?.Dispose();

            Parameters.ColorTransfer.SetCustomProfile(profile);
            PaletteSummaryText = $"{profile.Name} ({profile.Illuminant})";
            StatusText = $"Extracted custom palette '{profile.Name}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Palette extraction failed: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("Palette Extraction Failed", $"Could not extract palette from '{path}':\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void SetPreviewCap(PreviewResolutionCap cap)
    {
        PreviewCap = cap;
    }

    [RelayCommand]
    private void SwitchBaseline(BaselineMode mode)
    {
        if (ActiveBaselineMode != mode)
        {
            ActiveBaselineMode = mode;
        }
        else
        {
            ApplyBaselineSwitch(mode);
        }
    }

    [RelayCommand]
    private void ResetAll()
    {
        Parameters.ResetAllToBaseline();
        StatusText = "Reset all parameters to active baseline.";
    }

    [RelayCommand(CanExecute = nameof(CanResetStage))]
    private void ResetStage(PrimaryStageId stageId)
    {
        Parameters.ResetStage(stageId);
        StatusText = $"Reset {PipelineStageRegistry.GetStage(stageId).DisplayName} parameters.";
    }

    [RelayCommand(CanExecute = nameof(CanResetActiveStage))]
    private void ResetActiveStage()
    {
        ResetStage(ActiveStageId);
    }

    private static bool CanResetStage(PrimaryStageId stageId) => stageId != PrimaryStageId.Input;

    [RelayCommand]
    private void ToggleAutoRun()
    {
        IsAutoRunEnabled = !IsAutoRunEnabled;
        StatusText = IsAutoRunEnabled ? "Auto-Run enabled." : "Auto-Run paused — use Run to update.";
    }

    [RelayCommand(CanExecute = nameof(CanRunNow))]
    private async Task RunNowAsync()
    {
        if (!Engine.HasImage)
        {
            return;
        }

        if (Engine.PendingCanonicalOptions is null && Engine.LastRenderedCanonicalOptions is null)
        {
            bool previous = Engine.IsAutoRunEnabled;
            Engine.IsAutoRunEnabled = true;
            try
            {
                await Engine.RequestRender(Parameters.BuildOptions(), immediate: true);
            }
            finally
            {
                Engine.IsAutoRunEnabled = previous;
            }

            return;
        }

        await Engine.RunNowAsync();
    }

    private bool CanRunNow() => HasImage && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancelRender))]
    private void CancelRender()
    {
        Engine.CancelActiveRender();
        CancelActiveExport();
    }

    private bool CanCancelRender() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
    private void Undo()
    {
        Parameters.Undo();
        StatusText = "Undid parameter change.";
    }

    private bool CanExecuteUndo() => Parameters.CanUndo;

    [RelayCommand(CanExecute = nameof(CanExecuteRedo))]
    private void Redo()
    {
        Parameters.Redo();
        StatusText = "Redid parameter change.";
    }

    private bool CanExecuteRedo() => Parameters.CanRedo;

    [RelayCommand]
    private void PinCurrentStage()
    {
        if (SelectedStage is null)
        {
            return;
        }

        SetPinnedStage(PinnedStageId == SelectedStage.StageId ? null : SelectedStage.StageId);
    }

    [RelayCommand]
    private void TogglePinStage(StageCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        SetPinnedStage(PinnedStageId == card.StageId ? null : card.StageId);
    }

    [RelayCommand]
    private void ToggleCompare()
    {
        CompareMode = CompareMode == CompareMode.Off ? _lastNonOffCompareMode : CompareMode.Off;
    }

    [RelayCommand(CanExecute = nameof(CanToggleFullResPreview))]
    private void ToggleFullResPreview()
    {
        IsViewingFullRes = !IsViewingFullRes;
    }

    private bool CanToggleFullResPreview() => HasCachedFullRes;

    [RelayCommand]
    private void ToggleGridView()
    {
        IsGridView = !IsGridView;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    [RelayCommand]
    private void ToggleInspector()
    {
        IsInspectorOpen = !IsInspectorOpen;
    }

    [RelayCommand]
    private void ToggleHoverDiagnostics()
    {
        ShowHoverDiagnostics = !ShowHoverDiagnostics;
    }

    [RelayCommand]
    private void ApplyLookShortcut(string? lookKey)
    {
        if (string.IsNullOrWhiteSpace(lookKey))
        {
            return;
        }

        IsInspectorOpen = true;
        switch (lookKey)
        {
            case "looser-wash":
                SelectPrimaryStage(PrimaryStageId.LodAbstraction);
                StatusText = "Jumped to 5. LOD Abstraction for looser brushstroke washes.";
                break;
            case "paper-tooth":
                SelectSubStage(StageViewId.FinishingPaperGranulation);
                InspectorSectionFocusRequested?.Invoke("PaperGranulation");
                StatusText = "Jumped to 8. Finishing → Paper Granulation for stronger paper tooth.";
                break;
            case "crisper-edges":
                SelectSubStage(StageViewId.FinishingEdgeDarkening);
                InspectorSectionFocusRequested?.Invoke("EdgeDarkening");
                StatusText = "Jumped to 8. Finishing → Edge Darkening for crisper pigment borders.";
                break;
            case "richer-color":
                SelectPrimaryStage(PrimaryStageId.ColorTransfer);
                StatusText = "Jumped to 2. Color Transfer for richer palette & pigment shift.";
                break;
            case "focus-subject":
                SelectSubStage(StageViewId.SaliencyRcMap);
                InspectorSectionFocusRequested?.Invoke("Saliency");
                StatusText = "Jumped to 3. Saliency to refine subject vs. background focus.";
                break;
            case "paper-gaps":
                SelectPrimaryStage(PrimaryStageId.HandTremor);
                StatusText = "Jumped to 7. Hand Tremor for dry-brush paper gaps & edge wobble.";
                break;
        }
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        string stem = Engine.SourcePath is not null
            ? $"{Path.GetFileNameWithoutExtension(Engine.SourcePath)}_preset"
            : "watercolor_preset";

        string? path = await _fileDialogs.PickSavePresetAsync(stem);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Dictionary<WatercolorStep, double>? timingsMs = null;
            if (Engine.LastResult is { StepTimings.Count: > 0 } lastResult)
            {
                timingsMs = new Dictionary<WatercolorStep, double>(lastResult.StepTimings.Count);
                foreach ((WatercolorStep step, WatercolorStepTiming timing) in lastResult.StepTimings)
                {
                    timingsMs[step] = timing.Elapsed.TotalMilliseconds;
                }
            }

            var preset = new PresetDocument
            {
                SchemaVersion = PresetDocument.CurrentSchemaVersion,
                Name = Path.GetFileNameWithoutExtension(path) is { Length: > 0 } name ? name : "Watercolor Preset",
                CalibrationReferenceDimension = Engine.HasImage
                    ? Engine.NativeSize.LinearDimension
                    : WatercolorOptions.DefaultReferenceDimension,
                PaletteMode = Parameters.ColorTransfer.PaletteMode,
                Options = Parameters.BuildOptions(),
                StepTimingsMs = timingsMs,
            };

            await PresetSerializer.SaveToFileAsync(preset, path);
            StatusText = $"Saved preset '{Path.GetFileName(path)}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to save preset: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("Failed to Save Preset", $"Could not save preset to '{path}':\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadPresetAsync()
    {
        string? path = await _fileDialogs.PickOpenPresetAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            PresetDocument preset = await PresetSerializer.LoadFromFileAsync(path);
            if (_disposed)
            {
                return;
            }

            WatercolorOptions optionsToLoad = preset.Options;
            if (Engine.HasImage &&
                PresetSerializer.ShouldAutoScaleForImageSize(preset, Engine.NativeSize))
            {
                double currentDim = Engine.NativeSize.LinearDimension;
                bool scaleToSize = await _fileDialogs.ConfirmAsync(
                    "Scale Preset Parameters?",
                    $"This preset was calibrated at reference dimension {preset.CalibrationReferenceDimension:0} px, while the active image is {Engine.NativeSize.Width}×{Engine.NativeSize.Height} ({currentDim:0} px).\n\nScale spatial parameters to match the current image?",
                    "Scale to Image",
                    "Keep Exact Values");

                optionsToLoad = PresetSerializer.AdaptForImageSize(preset, Engine.NativeSize, scaleToSize);
            }

            Parameters.LoadFromOptions(optionsToLoad, recordUndo: true, paletteModeHint: preset.PaletteMode);
            StatusText = $"Loaded preset '{preset.Name}'.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load preset: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("Failed to Load Preset", $"Could not load preset from '{path}':\n{ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunExport))]
    private async Task RenderFullResolutionAsync()
    {
        if (!Engine.HasImage)
        {
            return;
        }

        CancellationTokenSource cts = ReplaceExportCts();
        IsBusy = true;
        ProgressText = "Rendering full resolution...";
        StatusText = $"Rendering full resolution ({Engine.NativeSize.Width}×{Engine.NativeSize.Height})...";

        try
        {
            WatercolorOptions canonical = Parameters.BuildOptions();
            WatercolorResult result = await RunFullResolutionInternalAsync(canonical, cts.Token);
            SetCachedFullRes(canonical, result);

            double totalMs = result.StepTimings.Values.Sum(static t => t.Elapsed.TotalMilliseconds);
            ProgressText = $"Full-res done ({totalMs:0} ms)";
            StatusText = $"Full-resolution render complete ({Engine.NativeSize.Width}×{Engine.NativeSize.Height}) — toggle 'Preview Full-Res' to inspect.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Cancelled";
            StatusText = "Full-resolution render cancelled.";
        }
        catch (Exception ex)
        {
            ProgressText = "Error";
            StatusText = $"Full-resolution render failed: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("Full-Resolution Render Failed", ex.Message);
        }
        finally
        {
            FinishExportCts(cts);
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunExport))]
    private async Task ExportFinalFullResAsync()
    {
        if (!Engine.HasImage)
        {
            return;
        }

        string suggested = BuildSuggestedExportFileName("watercolor");
        string? path = await _fileDialogs.PickSaveFinalImageAsync(suggested);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        CancellationTokenSource cts = ReplaceExportCts();
        IsBusy = true;
        ProgressText = "Rendering full resolution for export...";
        StatusText = $"Exporting full-res watercolor to {Path.GetFileName(path)}...";

        try
        {
            WatercolorOptions canonical = Parameters.BuildOptions();
            WatercolorResult result = await GetOrRenderFullResolutionAsync(canonical, cts.Token);

            ProgressText = $"Saving {Path.GetFileName(path)}...";
            await ExportService.ExportFinalImageAsync(result.Image, path, cts.Token);

            ProgressText = "Export complete";
            StatusText = $"Exported full-resolution image ({result.Image.Width}×{result.Image.Height}) to {Path.GetFileName(path)}.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Cancelled";
            StatusText = "Full-resolution export cancelled.";
        }
        catch (Exception ex)
        {
            ProgressText = "Error";
            StatusText = $"Export failed: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("Export Failed", $"Could not export '{path}':\n{ex.Message}");
        }
        finally
        {
            FinishExportCts(cts);
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunExport))]
    private async Task ExportFinalPreviewResAsync()
    {
        if (!Engine.HasImage)
        {
            return;
        }

        string suggested = BuildSuggestedExportFileName("watercolor_preview");
        string? path = await _fileDialogs.PickSaveFinalImageAsync(suggested);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        CancellationTokenSource cts = ReplaceExportCts();
        IsBusy = true;
        ProgressText = "Preparing preview export...";
        StatusText = $"Exporting preview image to {Path.GetFileName(path)}...";

        try
        {
            WatercolorResult? result = Engine.LastResult;
            if (result is null || Engine.StaleStages.Count > 0)
            {
                result = await Engine.RunNowAsync();
            }

            cts.Token.ThrowIfCancellationRequested();
            if (result is null)
            {
                StatusText = "Preview export aborted — no rendered result available.";
                return;
            }

            await ExportService.ExportFinalImageAsync(result.Image, path, cts.Token);
            ProgressText = "Preview exported";
            StatusText = $"Exported preview image ({result.Image.Width}×{result.Image.Height}) to {Path.GetFileName(path)}.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Cancelled";
            StatusText = "Preview export cancelled.";
        }
        catch (Exception ex)
        {
            ProgressText = "Error";
            StatusText = $"Export failed: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("Preview Export Failed", $"Could not export '{path}':\n{ex.Message}");
        }
        finally
        {
            FinishExportCts(cts);
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunExport))]
    private async Task ExportAllStagesAsync()
    {
        if (!Engine.HasImage || Engine.NativeSource is null)
        {
            return;
        }

        string? folder = await _fileDialogs.PickExportFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        CancellationTokenSource cts = ReplaceExportCts();
        IsBusy = true;
        ProgressText = "Rendering full resolution for all-stage export...";
        StatusText = $"Exporting all 24 stages + preset.json to {folder}...";

        try
        {
            WatercolorOptions canonical = Parameters.BuildOptions();
            WatercolorResult result = await GetOrRenderFullResolutionAsync(canonical, cts.Token);

            var progress = new Progress<(int Completed, int Total, string FileName)>(report =>
            {
                if (!_disposed)
                {
                    ProgressText = $"Exporting {report.Completed}/{report.Total}: {report.FileName}";
                }
            });

            await ExportService.ExportAllStagesAsync(
                Engine.NativeSource,
                result,
                canonical,
                folder,
                includePresetSidecar: true,
                calibrationReferenceDimension: Engine.NativeSize.LinearDimension,
                paletteMode: Parameters.ColorTransfer.PaletteMode,
                progress: progress,
                ct: cts.Token);

            ProgressText = "All stages exported";
            StatusText = $"Exported 24 stage PNGs + preset.json to {folder}.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Cancelled";
            StatusText = "All-stages export cancelled.";
        }
        catch (Exception ex)
        {
            ProgressText = "Error";
            StatusText = $"Export all stages failed: {ex.Message}";
            await _fileDialogs.ShowErrorAsync("All-Stages Export Failed", $"Could not export stages to '{folder}':\n{ex.Message}");
        }
        finally
        {
            FinishExportCts(cts);
            IsBusy = false;
        }
    }

    private bool CanRunExport() => HasImage && !IsBusy;

    [RelayCommand]
    private void SelectPrimaryStage(PrimaryStageId stageId)
    {
        StageCardViewModel? match = FindStageCard(stageId);
        if (match is not null)
        {
            SelectedStage = match;
        }
    }

    [RelayCommand]
    private void SelectSubStage(StageViewId viewId)
    {
        StageViewDescriptor descriptor = PipelineStageRegistry.GetView(viewId);
        if (SelectedStage is null || SelectedStage.StageId != descriptor.ParentStage)
        {
            SelectPrimaryStage(descriptor.ParentStage);
        }

        foreach (SubStageItemViewModel sub in SubStages)
        {
            if (sub.ViewId == viewId)
            {
                bool alreadySelected = ReferenceEquals(SelectedSubStage, sub);
                SelectedSubStage = sub;
                if (alreadySelected)
                {
                    RequestInspectorSectionFocusIfApplicable(viewId);
                }

                return;
            }
        }

        if (SelectedStage is not null)
        {
            SelectedStage.ActiveViewId = viewId;
            SyncVisualizationModesForActiveView();
            RefreshSelectedStageCardBitmap();
            RefreshViewportDisplay();
            RequestInspectorSectionFocusIfApplicable(viewId);
        }
    }

    [RelayCommand]
    private void ReseedRandomColors()
    {
        _regionRandomSeed = unchecked(_regionRandomSeed + 1);
        RefreshAllStageCardBitmaps();
        RefreshViewportDisplay();
        StatusText = $"Reseeded region color map (seed {_regionRandomSeed}).";
    }

    [RelayCommand]
    private void SetCompareMode(CompareMode mode)
    {
        CompareMode = mode;
    }

    [RelayCommand]
    private void SetCompareReference(CompareReferenceKind kind)
    {
        CompareReferenceKind = kind;
    }

    /// <summary>
    /// Releases managed pipeline resources and owned bitmaps cleanly.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelActiveExport();

        Engine.RenderStarted -= OnEngineRenderStarted;
        Engine.StepProgress -= OnEngineStepProgress;
        Engine.RenderCompleted -= OnEngineRenderCompleted;
        Engine.RenderCancelled -= OnEngineRenderCancelled;
        Engine.RenderFailed -= OnEngineRenderFailed;
        Engine.StalenessChanged -= OnEngineStalenessChanged;

        Parameters.OptionsChanged -= OnParametersOptionsChanged;
        Parameters.PropertyChanged -= OnParametersPropertyChanged;
        Parameters.ColorTransfer.PropertyChanged -= OnBypassableStagePropertyChanged;
        Parameters.LodAbstraction.PropertyChanged -= OnBypassableStagePropertyChanged;
        Parameters.WetInWet.PropertyChanged -= OnBypassableStagePropertyChanged;
        Parameters.HandTremor.PropertyChanged -= OnBypassableStagePropertyChanged;

        _referencePaletteThumbnail?.Dispose();
        _referencePaletteThumbnail = null;

        ClearViewportBitmaps();
        foreach (StageCardViewModel card in Stages)
        {
            card.Dispose();
        }

        Engine.Dispose();
    }

    private void LoadLoadedImage(SequentialMatrix<Rgb8> matrix, string? sourcePath, string displayName)
    {
        CancelActiveExport();
        Engine.CancelActiveRender();
        InvalidateFullResCache();
        ClearViewportBitmaps();
        ClearAllStageCardBitmaps();
        ClearHoverPixel();
        ToneCurveHistogram = null;

        Engine.LoadImage(matrix, sourcePath);

        HasImage = true;
        SourceFileDisplayText = displayName;
        NativeResolutionText = $"{Engine.NativeSize.Width} × {Engine.NativeSize.Height}";
        PreviewResolutionText = $"{Engine.PreviewSize.Width} × {Engine.PreviewSize.Height}";
        PaletteSummaryText = "—";
        SegmentationStatsText = "Regions: — • Boundaries: —";
        WetInWetStatsText = "Qualifying Boundaries: —";
        StageTimingSummaryText = "Pending...";

        _isLoadingImage = true;
        try
        {
            WatercolorOptions baseline = Engine.ComputeBaseline(ActiveBaselineMode);
            Parameters.SetBaselineOnly(ActiveBaselineMode, baseline);
            Parameters.LoadFromOptions(baseline, recordUndo: false);
            UpdateBaselineSummaryText();
        }
        finally
        {
            _isLoadingImage = false;
        }

        if (SelectedStage is null || SelectedStage.StageId == PrimaryStageId.Input)
        {
            SelectedStage = Stages[^1];
        }

        UpdateInputStageCardBitmap();
        RefreshViewportDisplay();
        ImageLoaded?.Invoke(this, EventArgs.Empty);

        StatusText = $"Loaded {displayName} ({NativeResolutionText}).";
        _ = Engine.RequestRender(Parameters.BuildOptions(), immediate: true);
    }

    private void ApplyPreviewCapChange(PreviewResolutionCap cap)
    {
        Engine.SetPreviewCap(cap);
        PreviewResolutionText = Engine.HasImage
            ? $"{Engine.PreviewSize.Width} × {Engine.PreviewSize.Height}"
            : "—";

        if (!Engine.HasImage)
        {
            return;
        }

        ClearHoverPixel();
        UpdateInputStageCardBitmap();
        RefreshViewportDisplay();
        StatusText = $"Preview cap set to {EnumDisplayNameConverterLabel(cap)} ({PreviewResolutionText}).";
        _ = Engine.RequestRender(Parameters.BuildOptions(), immediate: true);
    }

    private void ApplyBaselineSwitch(BaselineMode mode)
    {
        _isApplyingBaseline = true;
        try
        {
            ActiveBaselineMode = mode;
            UpdateBaselineSummaryText();

            if (!Engine.HasImage && mode != BaselineMode.Default)
            {
                StatusText = $"Baseline mode set to {BaselineSummaryText}.";
                return;
            }

            WatercolorOptions baseline = Engine.HasImage
                ? Engine.ComputeBaseline(mode)
                : WatercolorOptions.Default;

            Parameters.SwitchBaseline(mode, baseline);
            StatusText = $"Switched baseline to {BaselineSummaryText} (Ctrl+Z to undo).";
        }
        finally
        {
            _isApplyingBaseline = false;
        }
    }

    private void HandleSelectedStageChanged(StageCardViewModel? stage)
    {
        if (_isSyncingStageState)
        {
            return;
        }

        _isSyncingStageState = true;
        try
        {
            for (int i = 0; i < Stages.Count; i++)
            {
                Stages[i].IsSelected = ReferenceEquals(Stages[i], stage);
            }

            SubStages.Clear();
            if (stage is not null)
            {
                for (int i = 0; i < stage.SubStages.Count; i++)
                {
                    SubStages.Add(stage.SubStages[i]);
                }

                ShowSubStageDrawer = stage.HasSubStages && SubStages.Count > 0;

                SubStageItemViewModel? activeSub = null;
                for (int i = 0; i < SubStages.Count; i++)
                {
                    SubStageItemViewModel sub = SubStages[i];
                    bool isMatch = sub.ViewId == stage.ActiveViewId;
                    sub.IsSelected = isMatch;
                    if (isMatch)
                    {
                        activeSub = sub;
                    }
                }

                if (activeSub is null && SubStages.Count > 0)
                {
                    activeSub = SubStages[0];
                    activeSub.IsSelected = true;
                    stage.ActiveViewId = activeSub.ViewId;
                }

                SelectedSubStage = activeSub;
            }
            else
            {
                ShowSubStageDrawer = false;
                SelectedSubStage = null;
            }
        }
        finally
        {
            _isSyncingStageState = false;
        }

        NotifyActiveStagePropertiesChanged();
        SyncVisualizationModesForActiveView();
        RefreshViewportDisplay();
    }

    private void HandleSelectedSubStageChanged(SubStageItemViewModel? subStage)
    {
        if (_isSyncingStageState || subStage is null)
        {
            return;
        }

        _isSyncingStageState = true;
        try
        {
            if (SelectedStage is null || SelectedStage.StageId != subStage.ParentStage)
            {
                StageCardViewModel? parent = FindStageCard(subStage.ParentStage);
                if (parent is not null)
                {
                    _selectedStage = parent;
                    OnPropertyChanged(nameof(SelectedStage));
                    for (int i = 0; i < Stages.Count; i++)
                    {
                        Stages[i].IsSelected = ReferenceEquals(Stages[i], parent);
                    }

                    SubStages.Clear();
                    for (int i = 0; i < parent.SubStages.Count; i++)
                    {
                        SubStages.Add(parent.SubStages[i]);
                    }

                    ShowSubStageDrawer = parent.HasSubStages && SubStages.Count > 0;
                    NotifyActiveStagePropertiesChanged();
                }
            }

            for (int i = 0; i < SubStages.Count; i++)
            {
                SubStages[i].IsSelected = ReferenceEquals(SubStages[i], subStage);
            }

            if (SelectedStage is not null)
            {
                SelectedStage.ActiveViewId = subStage.ViewId;
            }
        }
        finally
        {
            _isSyncingStageState = false;
        }

        SyncVisualizationModesForActiveView();
        RefreshSelectedStageCardBitmap();
        RefreshViewportDisplay();
        RequestInspectorSectionFocusIfApplicable(subStage.ViewId);
    }

    private void HandleVisualizationModeChanged(VisualizationMode mode)
    {
        StageViewId activeView = GetActiveStageViewId();
        if (!PipelineStageRegistry.IsValidMode(activeView, mode))
        {
            VisualizationMode fallback = PipelineStageRegistry.GetView(activeView).DefaultMode;
            _isSyncingVisualizationMode = true;
            try
            {
                SelectedVisualizationMode = fallback;
            }
            finally
            {
                _isSyncingVisualizationMode = false;
            }

            mode = fallback;
        }

        _viewModes[activeView] = mode;
        ShowReseedButton = mode == VisualizationMode.RegionRandomColor;

        RefreshSelectedStageCardBitmap();
        RefreshViewportDisplay();
    }

    private void SyncVisualizationModesForActiveView()
    {
        StageViewId activeView = GetActiveStageViewId();
        StageViewDescriptor descriptor = PipelineStageRegistry.GetView(activeView);
        VisualizationMode mode = GetVisualizationModeForView(activeView);

        _isSyncingVisualizationMode = true;
        try
        {
            AvailableVisualizationModes = descriptor.ValidModes;
            SelectedVisualizationMode = mode;
        }
        finally
        {
            _isSyncingVisualizationMode = false;
        }

        ShowReseedButton = mode == VisualizationMode.RegionRandomColor;
    }

    private void OnParametersOptionsChanged(object? sender, OptionsChangedEventArgs e)
    {
        InvalidateFullResCache();

        if (_isLoadingImage || !Engine.HasImage)
        {
            return;
        }

        _ = Engine.RequestRender(e.Options, immediate: e.IsImmediate);
    }

    private void OnParametersPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PipelineParametersViewModel.CanUndo) or nameof(PipelineParametersViewModel.CanRedo))
        {
            OnPropertyChanged(e.PropertyName);
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnEngineRenderStarted(object? sender, RenderStartedEventArgs e)
    {
        _uiDispatcher(() =>
        {
            if (_disposed)
            {
                return;
            }

            IsBusy = true;
            ProgressText = "Rendering...";
            StatusText = $"Rendering preview ({e.PreviewSize.Width}×{e.PreviewSize.Height})...";

            for (int i = 0; i < Stages.Count; i++)
            {
                StageCardViewModel card = Stages[i];
                card.IsRunning = card.StageId == PrimaryStageId.ColorTransfer;
                for (int j = 0; j < card.SubStages.Count; j++)
                {
                    card.SubStages[j].IsRunning = false;
                }
            }
        });
    }

    private void OnEngineStepProgress(object? sender, WatercolorStepProgress progress)
    {
        _uiDispatcher(() =>
        {
            if (_disposed)
            {
                return;
            }

            PrimaryStageId completedStageId = PipelineStageRegistry.GetStageForStep(progress.Step);
            PipelineStageDescriptor stageDescriptor = PipelineStageRegistry.GetStage(completedStageId);

            ProgressText = progress.Bypassed
                ? $"{stageDescriptor.DisplayName}: bypassed"
                : progress.FromCache
                    ? $"{stageDescriptor.DisplayName}: cached"
                    : $"{stageDescriptor.DisplayName}: {progress.Elapsed.TotalMilliseconds:0} ms";

            string stepText = FormatStepTimingText(progress.Elapsed, progress.FromCache, progress.Bypassed);

            for (int i = 0; i < Stages.Count; i++)
            {
                StageCardViewModel card = Stages[i];
                for (int j = 0; j < card.SubStages.Count; j++)
                {
                    SubStageItemViewModel sub = card.SubStages[j];
                    if (PipelineStageRegistry.GetView(sub.ViewId).MappedStep == progress.Step)
                    {
                        sub.IsRunning = false;
                        sub.IsBypassed = progress.Bypassed;
                        sub.IsFromCache = !progress.Bypassed && progress.FromCache;
                        sub.TimingText = stepText;
                    }
                }
            }

            int completedIndex = (int)completedStageId;
            for (int i = 0; i < Stages.Count; i++)
            {
                Stages[i].IsRunning = i == completedIndex + 1 && i < Stages.Count;
            }
        });
    }

    private void OnEngineRenderCompleted(object? sender, RenderCompletedEventArgs e)
    {
        _uiDispatcher(() =>
        {
            if (_disposed || !ReferenceEquals(Engine.LastResult, e.Result))
            {
                return;
            }

            IsBusy = false;
            ProgressText = $"{e.TotalElapsed.TotalMilliseconds:0} ms";
            StatusText = $"Rendered {Engine.PreviewSize.Width}×{Engine.PreviewSize.Height} in {e.TotalElapsed.TotalMilliseconds:0} ms.";

            Parameters.ColorTransfer.ActiveMatchedPalette = e.Result.SelectedPalette;
            PaletteSummaryText = $"{e.Result.SelectedPalette.Name} ({e.Result.SelectedPalette.Illuminant})";

            int regionCount = e.Result.SegmentationGraph.RegionCount;
            int boundaryCount = e.Result.SegmentationGraph.Boundaries.Count;
            SegmentationStatsText = $"Regions: {regionCount:N0} • Boundaries: {boundaryCount:N0}";

            int qualBoundaries = 0;
            IReadOnlyList<bool> qualFlags = e.Result.WetInWet.QualifyingBoundaries;
            for (int i = 0; i < qualFlags.Count; i++)
            {
                if (qualFlags[i])
                {
                    qualBoundaries++;
                }
            }

            WetInWetStatsText = $"Qualifying Boundaries: {qualBoundaries:N0} / {qualFlags.Count:N0}";

            int cachedCount = 0;
            int executedCount = 0;
            foreach (WatercolorStepTiming timing in e.Result.StepTimings.Values)
            {
                if (!timing.Bypassed)
                {
                    if (timing.FromCache)
                    {
                        cachedCount++;
                    }
                    else
                    {
                        executedCount++;
                    }
                }
            }

            StageTimingSummaryText = $"{e.TotalElapsed.TotalMilliseconds:0} ms ({cachedCount} cached, {executedCount} run)";
            ToneCurveHistogram = StageVisualizers.ComputeLuminanceHistogram(e.Result.DensityAdjusted ?? Engine.PreviewSource);

            ApplyResultToStageCards(e.Result, e.EffectiveOptions);
            RefreshViewportDisplay();
            OnPropertyChanged(nameof(HasPendingChanges));
            OnPropertyChanged(nameof(IsRunHighlighted));
        });
    }

    private void OnEngineRenderCancelled(object? sender, RenderCancelledEventArgs e)
    {
        _uiDispatcher(() =>
        {
            if (_disposed)
            {
                return;
            }

            IsBusy = false;
            ProgressText = "Cancelled";
            StatusText = "Preview render cancelled.";
            ClearRunningStates();
        });
    }

    private void OnEngineRenderFailed(object? sender, RenderFailedEventArgs e)
    {
        _uiDispatcher(() =>
        {
            if (_disposed)
            {
                return;
            }

            IsBusy = false;
            ProgressText = "Failed";
            StatusText = $"Render failed: {e.Exception.Message}";
            ClearRunningStates();
        });
    }

    private void OnEngineStalenessChanged(object? sender, StalenessChangedEventArgs e)
    {
        _uiDispatcher(() =>
        {
            if (_disposed)
            {
                return;
            }

            for (int i = 0; i < Stages.Count; i++)
            {
                StageCardViewModel card = Stages[i];
                bool isStale = e.StaleStages.Contains(card.StageId);
                card.IsStale = isStale;
                if (isStale && !card.IsRunning && !card.IsBypassed)
                {
                    card.IsFromCache = false;
                    card.StatusBadgeText = "Stale";
                }
                else if (!isStale && card.StatusBadgeText == "Stale")
                {
                    card.StatusBadgeText = card.IsBypassed ? "Bypassed" : card.IsFromCache ? "Cached" : string.Empty;
                }

                for (int j = 0; j < card.SubStages.Count; j++)
                {
                    SubStageItemViewModel sub = card.SubStages[j];
                    if (PipelineStageRegistry.GetView(sub.ViewId).MappedStep is WatercolorStep step)
                    {
                        sub.IsStale = e.StaleSteps.Contains(step);
                    }
                }
            }

            OnPropertyChanged(nameof(HasPendingChanges));
            OnPropertyChanged(nameof(IsRunHighlighted));
        });
    }

    private void ApplyResultToStageCards(WatercolorResult result, WatercolorOptions effectiveOptions)
    {
        if (Engine.PreviewSource is null)
        {
            return;
        }

        IReadOnlySet<PrimaryStageId> staleStages = Engine.StaleStages;
        IReadOnlySet<WatercolorStep> staleSteps = Engine.StaleSteps;

        for (int i = 0; i < Stages.Count; i++)
        {
            StageCardViewModel card = Stages[i];
            PipelineStageDescriptor descriptor = PipelineStageRegistry.GetStage(card.StageId);

            card.IsRunning = false;
            card.IsPinned = _pinnedStageId == card.StageId;

            if (descriptor.MappedSteps.Count == 0)
            {
                card.TimingText = "Input";
                card.IsFromCache = false;
                card.IsBypassed = false;
                card.IsStale = false;
                card.StatusBadgeText = string.Empty;
            }
            else
            {
                bool allBypassed = true;
                bool allCachedOrBypassed = true;
                bool anyCached = false;
                double elapsedMs = 0.0;

                for (int s = 0; s < descriptor.MappedSteps.Count; s++)
                {
                    WatercolorStep step = descriptor.MappedSteps[s];
                    if (result.StepTimings.TryGetValue(step, out WatercolorStepTiming timing))
                    {
                        if (!timing.Bypassed)
                        {
                            allBypassed = false;
                            if (timing.FromCache)
                            {
                                anyCached = true;
                            }
                            else
                            {
                                allCachedOrBypassed = false;
                                elapsedMs += timing.Elapsed.TotalMilliseconds;
                            }
                        }
                    }
                    else
                    {
                        allBypassed = false;
                        allCachedOrBypassed = false;
                    }
                }

                bool isStale = staleStages.Contains(card.StageId);
                card.IsStale = isStale;
                card.IsBypassed = allBypassed;
                card.IsFromCache = !allBypassed && allCachedOrBypassed && anyCached;

                if (allBypassed)
                {
                    card.TimingText = "Bypassed";
                    card.StatusBadgeText = "Bypassed";
                }
                else if (card.IsFromCache)
                {
                    card.TimingText = "Cached";
                    card.StatusBadgeText = isStale ? "Stale" : "Cached";
                }
                else
                {
                    card.TimingText = elapsedMs < 1.0 ? "<1 ms" : $"{elapsedMs:0} ms";
                    card.StatusBadgeText = isStale ? "Stale" : string.Empty;
                }
            }

            for (int j = 0; j < card.SubStages.Count; j++)
            {
                SubStageItemViewModel sub = card.SubStages[j];
                sub.IsRunning = false;
                if (PipelineStageRegistry.GetView(sub.ViewId).MappedStep is WatercolorStep step &&
                    result.StepTimings.TryGetValue(step, out WatercolorStepTiming timing))
                {
                    sub.IsBypassed = timing.Bypassed;
                    sub.IsFromCache = !timing.Bypassed && timing.FromCache;
                    sub.IsStale = staleSteps.Contains(step);
                    sub.TimingText = FormatStepTimingText(timing.Elapsed, timing.FromCache, timing.Bypassed);
                }
            }

            UpdateStageCardBitmaps(card, result, effectiveOptions);
        }
    }

    private void UpdateInputStageCardBitmap()
    {
        if (Engine.PreviewSource is null || Stages.Count == 0)
        {
            return;
        }

        StageCardViewModel inputCard = Stages[0];
        inputCard.TimingText = "Input";
        inputCard.IsStale = false;
        inputCard.IsBypassed = false;
        inputCard.IsFromCache = false;
        inputCard.StatusBadgeText = string.Empty;

        UpdateStageCardBitmapsFromMatrix(inputCard, Engine.PreviewSource);
    }

    private void RefreshSelectedStageCardBitmap()
    {
        if (SelectedStage is null || Engine.PreviewSource is null)
        {
            return;
        }

        if (SelectedStage.StageId == PrimaryStageId.Input)
        {
            UpdateInputStageCardBitmap();
            return;
        }

        if (Engine.LastResult is not null)
        {
            UpdateStageCardBitmaps(
                SelectedStage,
                Engine.LastResult,
                Engine.LastRenderedEffectiveOptions ?? Parameters.BuildOptions());
        }
    }

    private void RefreshAllStageCardBitmaps()
    {
        if (Engine.PreviewSource is null)
        {
            return;
        }

        UpdateInputStageCardBitmap();
        if (Engine.LastResult is null)
        {
            return;
        }

        WatercolorOptions options = Engine.LastRenderedEffectiveOptions ?? Parameters.BuildOptions();
        for (int i = 1; i < Stages.Count; i++)
        {
            UpdateStageCardBitmaps(Stages[i], Engine.LastResult, options);
        }
    }

    private void UpdateStageCardBitmaps(
        StageCardViewModel card,
        WatercolorResult result,
        WatercolorOptions effectiveOptions)
    {
        if (Engine.PreviewSource is null)
        {
            return;
        }

        if (card.StageId == PrimaryStageId.Input)
        {
            UpdateStageCardBitmapsFromMatrix(card, Engine.PreviewSource);
            return;
        }

        VisualizationMode mode = GetVisualizationModeForView(card.ActiveViewId);
        Matrix<Rgb8> matrix = StageVisualizers.RenderView(
            card.ActiveViewId,
            mode,
            Engine.PreviewSource,
            result,
            effectiveOptions,
            _regionRandomSeed);

        UpdateStageCardBitmapsFromMatrix(card, matrix);
    }

    private static void UpdateStageCardBitmapsFromMatrix(StageCardViewModel card, IReadOnlyMatrix<Rgb8> matrix)
    {
        WriteableBitmap? oldThumb = card.Thumbnail;
        WriteableBitmap nextThumb = MatrixBitmapConverter.BlitThumbnail(matrix, oldThumb, maxEdge: 320);
        card.Thumbnail = nextThumb;

        WriteableBitmap? oldGrid = card.GridBitmap;
        WriteableBitmap nextGrid = MatrixBitmapConverter.Blit(matrix, oldGrid);
        card.GridBitmap = nextGrid;

        if (ReferenceEquals(oldThumb, nextThumb) || ReferenceEquals(oldGrid, nextGrid))
        {
            card.NotifyBitmapsUpdated();
        }
    }

    private void RefreshViewportDisplay()
    {
        bool useFullRes = IsViewingFullRes && _cachedFullResResult is not null && Engine.NativeSource is not null;
        IReadOnlyMatrix<Rgb8>? sourceMatrix = useFullRes ? Engine.NativeSource : Engine.PreviewSource;
        WatercolorResult? activeResult = useFullRes ? _cachedFullResResult : Engine.LastResult;

        if (!Engine.HasImage || sourceMatrix is null)
        {
            ClearViewportBitmaps();
            return;
        }

        StageViewId activeView = GetActiveStageViewId();
        StageViewDescriptor activeDescriptor = PipelineStageRegistry.GetView(activeView);
        WatercolorOptions effectiveOptions = useFullRes
            ? (_cachedFullResOptions ?? Parameters.BuildOptions())
            : (Engine.LastRenderedEffectiveOptions ?? Parameters.BuildOptions());

        IReadOnlyMatrix<Rgb8>? primaryMatrix;
        if (activeView == StageViewId.Input)
        {
            primaryMatrix = sourceMatrix;
            PrimaryLabel = useFullRes ? $"{activeDescriptor.DisplayName} (Full Res)" : activeDescriptor.DisplayName;
        }
        else if (activeResult is WatercolorResult result)
        {
            primaryMatrix = StageVisualizers.RenderView(
                activeView,
                SelectedVisualizationMode,
                sourceMatrix,
                result,
                effectiveOptions,
                _regionRandomSeed);
            PrimaryLabel = useFullRes ? $"{activeDescriptor.DisplayName} (Full Res)" : activeDescriptor.DisplayName;
        }
        else
        {
            primaryMatrix = sourceMatrix;
            PrimaryLabel = $"{activeDescriptor.DisplayName} (Pending)";
        }

        _currentDisplayedMatrix = primaryMatrix;
        ApplyPrimaryBitmap(primaryMatrix);
        RefreshCompareBitmapOnly();
    }

    private void RefreshCompareBitmapOnly()
    {
        bool needsCompareBitmap = CompareMode != CompareMode.Off || IsHoldComparing;
        bool useFullRes = IsViewingFullRes && _cachedFullResResult is not null && Engine.NativeSource is not null;
        IReadOnlyMatrix<Rgb8>? sourceMatrix = useFullRes ? Engine.NativeSource : Engine.PreviewSource;

        if (!needsCompareBitmap || !Engine.HasImage || sourceMatrix is null)
        {
            _currentCompareMatrix = null;
            CompareLabel = string.Empty;
            DisposeCompareBitmap();
            return;
        }

        StageViewId activeView = GetActiveStageViewId();
        WatercolorOptions effectiveOptions = useFullRes
            ? (_cachedFullResOptions ?? Parameters.BuildOptions())
            : (Engine.LastRenderedEffectiveOptions ?? Parameters.BuildOptions());
        IReadOnlyMatrix<Rgb8>? compareMatrix = null;

        switch (CompareReferenceKind)
        {
            case CompareReferenceKind.Input:
                compareMatrix = sourceMatrix;
                CompareLabel = "Input";
                break;

            case CompareReferenceKind.PreviousStage:
            {
                StageViewId previousView = PipelineStageRegistry.GetPreviousView(activeView);
                StageViewDescriptor prevDesc = PipelineStageRegistry.GetView(previousView);
                CompareLabel = prevDesc.DisplayName;
                compareMatrix = RenderCompareView(previousView, effectiveOptions, useFullRes);
                break;
            }

            case CompareReferenceKind.PinnedStage:
            {
                if (_pinnedStageId is PrimaryStageId pinnedId)
                {
                    StageCardViewModel? pinnedCard = FindStageCard(pinnedId);
                    StageViewId pinnedView = pinnedCard?.ActiveViewId ?? PipelineStageRegistry.GetStage(pinnedId).DefaultView;
                    StageViewDescriptor pinnedDesc = PipelineStageRegistry.GetView(pinnedView);
                    CompareLabel = $"Pinned: {pinnedDesc.DisplayName}";
                    compareMatrix = RenderCompareView(pinnedView, effectiveOptions, useFullRes);
                }
                else
                {
                    CompareLabel = "No Stage Pinned";
                    compareMatrix = null;
                }

                break;
            }
        }

        _currentCompareMatrix = compareMatrix;
        if (compareMatrix is null)
        {
            DisposeCompareBitmap();
            return;
        }

        WriteableBitmap? previous = _compareBitmap;
        WriteableBitmap next = MatrixBitmapConverter.Blit(compareMatrix, previous);
        if (ReferenceEquals(previous, next))
        {
            ViewportRefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            CompareBitmap = next;
            previous?.Dispose();
        }
    }

    private IReadOnlyMatrix<Rgb8>? RenderCompareView(
        StageViewId viewId,
        WatercolorOptions effectiveOptions,
        bool useFullRes = false)
    {
        IReadOnlyMatrix<Rgb8>? sourceMatrix = useFullRes ? Engine.NativeSource : Engine.PreviewSource;
        WatercolorResult? activeResult = useFullRes ? _cachedFullResResult : Engine.LastResult;

        if (sourceMatrix is null)
        {
            return null;
        }

        if (viewId == StageViewId.Input)
        {
            return sourceMatrix;
        }

        if (activeResult is null)
        {
            return null;
        }

        return StageVisualizers.RenderView(
            viewId,
            GetVisualizationModeForView(viewId),
            sourceMatrix,
            activeResult,
            effectiveOptions,
            _regionRandomSeed);
    }

    private void ApplyPrimaryBitmap(IReadOnlyMatrix<Rgb8>? matrix)
    {
        if (matrix is null)
        {
            DisposePrimaryBitmap();
            return;
        }

        WriteableBitmap? previous = _primaryBitmap;
        WriteableBitmap next = MatrixBitmapConverter.Blit(matrix, previous);
        if (ReferenceEquals(previous, next))
        {
            ViewportRefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PrimaryBitmap = next;
            previous?.Dispose();
        }
    }

    private void ClearViewportBitmaps()
    {
        _currentDisplayedMatrix = null;
        _currentCompareMatrix = null;
        PrimaryLabel = string.Empty;
        CompareLabel = string.Empty;
        DisposePrimaryBitmap();
        DisposeCompareBitmap();
    }

    private void DisposePrimaryBitmap()
    {
        WriteableBitmap? old = _primaryBitmap;
        if (old is not null)
        {
            PrimaryBitmap = null;
            old.Dispose();
        }
    }

    private void DisposeCompareBitmap()
    {
        WriteableBitmap? old = _compareBitmap;
        if (old is not null)
        {
            CompareBitmap = null;
            old.Dispose();
        }
    }

    private void ClearAllStageCardBitmaps()
    {
        for (int i = 0; i < Stages.Count; i++)
        {
            Stages[i].ClearBitmaps();
        }
    }

    private void ClearRunningStates()
    {
        for (int i = 0; i < Stages.Count; i++)
        {
            StageCardViewModel card = Stages[i];
            card.IsRunning = false;
            for (int j = 0; j < card.SubStages.Count; j++)
            {
                card.SubStages[j].IsRunning = false;
            }
        }
    }

    private void SyncPinnedCardStates()
    {
        for (int i = 0; i < Stages.Count; i++)
        {
            Stages[i].IsPinned = _pinnedStageId.HasValue && Stages[i].StageId == _pinnedStageId.Value;
        }
    }

    private StageViewId GetActiveStageViewId() =>
        SelectedSubStage?.ViewId
        ?? SelectedStage?.ActiveViewId
        ?? StageViewId.FinishingFinalFxaa;

    private VisualizationMode GetVisualizationModeForView(StageViewId viewId)
    {
        if (_viewModes.TryGetValue(viewId, out VisualizationMode mode) &&
            PipelineStageRegistry.IsValidMode(viewId, mode))
        {
            return mode;
        }

        return PipelineStageRegistry.GetView(viewId).DefaultMode;
    }

    private StageCardViewModel? FindStageCard(PrimaryStageId stageId)
    {
        for (int i = 0; i < Stages.Count; i++)
        {
            if (Stages[i].StageId == stageId)
            {
                return Stages[i];
            }
        }

        return null;
    }

    private Task<WatercolorResult> GetOrRenderFullResolutionAsync(WatercolorOptions canonical, CancellationToken ct)
    {
        if (_cachedFullResResult is not null && Equals(_cachedFullResOptions, canonical))
        {
            return Task.FromResult(_cachedFullResResult);
        }

        return RenderAndCacheFullResolutionAsync(canonical, ct);
    }

    private async Task<WatercolorResult> RenderAndCacheFullResolutionAsync(WatercolorOptions canonical, CancellationToken ct)
    {
        WatercolorResult result = await RunFullResolutionInternalAsync(canonical, ct);
        SetCachedFullRes(canonical, result);
        return result;
    }

    private Task<WatercolorResult> RunFullResolutionInternalAsync(WatercolorOptions canonical, CancellationToken ct)
    {
        return Engine.RenderFullResolutionAsync(
            canonical,
            stepProgress => _uiDispatcher(() =>
            {
                if (!_disposed && IsBusy)
                {
                    string stageName = PipelineStageRegistry.GetStage(PipelineStageRegistry.GetStageForStep(stepProgress.Step)).DisplayName;
                    ProgressText = $"Full-res: {stageName} ({stepProgress.Elapsed.TotalMilliseconds:0} ms)";
                }
            }),
            ct);
    }

    private void SetCachedFullRes(WatercolorOptions canonical, WatercolorResult result)
    {
        _cachedFullResOptions = canonical;
        _cachedFullResResult = result;
        OnPropertyChanged(nameof(HasCachedFullRes));
        ToggleFullResPreviewCommand.NotifyCanExecuteChanged();
    }

    private void InvalidateFullResCache()
    {
        bool hadCache = _cachedFullResResult is not null;
        bool wasViewing = _isViewingFullRes;

        _cachedFullResResult = null;
        _cachedFullResOptions = null;

        if (wasViewing)
        {
            if (Engine.PreviewSize.Width > 0 &&
                Engine.NativeSize.Width > 0 &&
                Engine.PreviewSize.Width != Engine.NativeSize.Width)
            {
                Zoom *= (double)Engine.NativeSize.Width / Engine.PreviewSize.Width;
            }

            _isViewingFullRes = false;
            OnPropertyChanged(nameof(IsViewingFullRes));
            OnPropertyChanged(nameof(FullResToggleLabel));
        }

        if (hadCache || wasViewing)
        {
            OnPropertyChanged(nameof(HasCachedFullRes));
            ToggleFullResPreviewCommand.NotifyCanExecuteChanged();
        }

        if (wasViewing && !_isLoadingImage)
        {
            RefreshViewportDisplay();
            StatusText = "Parameter edited — reverted from full-res cache to interactive preview.";
        }
    }

    private void SetPinnedStage(PrimaryStageId? nextStage)
    {
        PinnedStageId = nextStage;
        if (nextStage is PrimaryStageId pinned)
        {
            string stageName = PipelineStageRegistry.GetStage(pinned).DisplayName;
            if (CompareReferenceKind != CompareReferenceKind.PinnedStage)
            {
                CompareReferenceKind = CompareReferenceKind.PinnedStage;
            }

            StatusText = CompareMode == CompareMode.Off
                ? $"Pinned '{stageName}' as A/B reference — press C or \\ to compare."
                : $"Pinned '{stageName}' as A/B comparison reference.";
        }
        else
        {
            StatusText = "Cleared pinned A/B comparison reference.";
        }
    }

    private void OnBypassableStagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is
            nameof(Core.Parameters.Stages.ColorTransferStageViewModel.EnableColorTransfer) or
            nameof(Core.Parameters.Stages.LodAbstractionStageViewModel.EnableLodAbstraction) or
            nameof(Core.Parameters.Stages.WetInWetStageViewModel.EnableWetInWet) or
            nameof(Core.Parameters.Stages.HandTremorStageViewModel.EnableHandTremor))
        {
            OnPropertyChanged(nameof(ActiveStageIsEnabled));
        }
    }

    private void NotifyActiveStagePropertiesChanged()
    {
        OnPropertyChanged(nameof(ActiveStageTitle));
        OnPropertyChanged(nameof(ActiveStageHeaderTitle));
        OnPropertyChanged(nameof(ActiveStageSubtitle));
        OnPropertyChanged(nameof(ActiveStageSummary));
        OnPropertyChanged(nameof(ActiveStageHasBypass));
        OnPropertyChanged(nameof(ActiveStageIsEnabled));
        OnPropertyChanged(nameof(ActiveStageId));
        OnPropertyChanged(nameof(CanResetActiveStage));
        OnPropertyChanged(nameof(IsCurrentStagePinned));
        ResetActiveStageCommand.NotifyCanExecuteChanged();
    }

    private StageViewId ResolveCompareStageViewId(StageViewId activeView)
    {
        return CompareReferenceKind switch
        {
            CompareReferenceKind.Input => StageViewId.Input,
            CompareReferenceKind.PreviousStage => PipelineStageRegistry.GetPreviousView(activeView),
            CompareReferenceKind.PinnedStage when _pinnedStageId is PrimaryStageId pinnedId =>
                FindStageCard(pinnedId)?.ActiveViewId ?? PipelineStageRegistry.GetStage(pinnedId).DefaultView,
            _ => activeView,
        };
    }

    private void RequestInspectorSectionFocusIfApplicable(StageViewId viewId)
    {
        string? sectionKey = viewId switch
        {
            StageViewId.FinishingEdgeDarkening => "EdgeDarkening",
            StageViewId.FinishingTurbulenceFlow => "TurbulenceFlow",
            StageViewId.FinishingToneCurve => "ToneCurve",
            StageViewId.FinishingPaperGranulation => "PaperGranulation",
            StageViewId.FinishingFinalFxaa => "Fxaa",
            _ => null,
        };

        if (sectionKey is not null)
        {
            InspectorSectionFocusRequested?.Invoke(sectionKey);
        }
    }

    private static SequentialMatrix<Rgb8> CreateSampleLandscapeMatrix(int width, int height)
    {
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            double ny = y / (double)(height - 1);
            for (int x = 0; x < width; x++)
            {
                double nx = x / (double)(width - 1);

                // Sky gradient (warm horizon to crisp cerulean)
                double r = 92.0 + 138.0 * Math.Pow(Math.Clamp(ny / 0.56, 0.0, 1.0), 1.4);
                double g = 148.0 + 78.0 * Math.Pow(Math.Clamp(ny / 0.56, 0.0, 1.0), 1.2);
                double b = 216.0 - 24.0 * Math.Clamp(ny / 0.56, 0.0, 1.0);

                // Soft sunlit clouds
                double cloudBand = Math.Exp(-Math.Pow((ny - 0.19) / 0.09, 2.0)) *
                                   (0.5 + 0.5 * Math.Sin(nx * 11.5 + 0.8) * Math.Cos(nx * 5.2));
                if (ny < 0.45 && cloudBand > 0.35)
                {
                    double w = Math.Clamp((cloudBand - 0.35) * 1.4, 0.0, 0.85);
                    r = r * (1 - w) + 250 * w;
                    g = g * (1 - w) + 244 * w;
                    b = b * (1 - w) + 236 * w;
                }

                // Distant misty hills
                double farHillY = 0.43 + 0.045 * Math.Sin(nx * 6.2 + 0.4) + 0.02 * Math.Cos(nx * 14.0);
                if (ny >= farHillY)
                {
                    r = 104 + 14 * Math.Sin(nx * 18.0);
                    g = 122 + 12 * Math.Cos(nx * 12.0);
                    b = 154;
                }

                // Mid-ground sunlit olive/sage hills
                double midHillY = 0.50 + 0.04 * Math.Cos(nx * 5.0 - 0.6) + 0.018 * Math.Sin(nx * 17.0);
                if (ny >= midHillY)
                {
                    double sun = 0.85 + 0.15 * Math.Sin(nx * 24.0 + ny * 14.0);
                    r = 84 * sun;
                    g = 128 * sun;
                    b = 76 * sun;
                }

                // Foreground grassy bank + reflective canal water
                double waterTop = 0.68 + 0.015 * Math.Sin(nx * 9.0);
                if (ny >= waterTop)
                {
                    double ripple = Math.Sin(ny * 140.0 + Math.Sin(nx * 20.0) * 2.5);
                    double reflection = Math.Exp(-Math.Pow((nx - 0.54) / 0.14, 2.0)) * 0.45;
                    r = 66 + 44 * reflection + 10 * ripple;
                    g = 112 + 32 * reflection + 12 * ripple;
                    b = 148 + 18 * reflection + 14 * ripple;
                }

                // Riverside cottage (terracotta roof + cream plaster wall + timber windows)
                bool inWall = nx is >= 0.44 and <= 0.65 && ny is >= 0.48 and <= 0.67;
                double roofSlope = 0.48 - 0.095 * (1.0 - Math.Abs((nx - 0.545) / 0.125));
                bool inRoof = nx is >= 0.42 and <= 0.67 && ny >= roofSlope && ny < 0.485;
                if (inRoof)
                {
                    double tile = 0.92 + 0.08 * Math.Sin(nx * 180.0);
                    r = 188 * tile;
                    g = 86 * tile;
                    b = 58 * tile;
                }
                else if (inWall)
                {
                    bool isDoor = nx is >= 0.525 and <= 0.565 && ny >= 0.56;
                    bool isLeftWin = nx is >= 0.468 and <= 0.502 && ny is >= 0.525 and <= 0.585;
                    bool isRightWin = nx is >= 0.588 and <= 0.622 && ny is >= 0.525 and <= 0.585;
                    if (isDoor || isLeftWin || isRightWin)
                    {
                        r = isDoor ? 76 : 44;
                        g = isDoor ? 52 : 64;
                        b = isDoor ? 38 : 82;
                    }
                    else
                    {
                        double shade = nx > 0.58 ? 0.88 : 1.0;
                        r = 242 * shade;
                        g = 230 * shade;
                        b = 204 * shade;
                    }
                }

                // Cypress trees flanking cottage + warm wildflower dots
                double dTree1 = Math.Pow((nx - 0.35) / 0.032, 2.0) + Math.Pow((ny - 0.50) / 0.16, 2.0);
                double dTree2 = Math.Pow((nx - 0.72) / 0.045, 2.0) + Math.Pow((ny - 0.52) / 0.14, 2.0);
                if (dTree1 < 1.0 || dTree2 < 1.0)
                {
                    double leaf = 0.85 + 0.15 * Math.Sin(nx * 90.0 + ny * 70.0);
                    r = 38 * leaf;
                    g = 82 * leaf;
                    b = 46 * leaf;
                }

                if (ny >= 0.60 && ny < waterTop && Math.Sin(nx * 95.0) * Math.Cos(ny * 110.0) > 0.84)
                {
                    r = 224;
                    g = 88;
                    b = 104;
                }

                matrix[x, y] = new Rgb8(
                    (byte)Math.Clamp((int)Math.Round(r), 0, 255),
                    (byte)Math.Clamp((int)Math.Round(g), 0, 255),
                    (byte)Math.Clamp((int)Math.Round(b), 0, 255));
            }
        }

        return matrix;
    }

    private CancellationTokenSource ReplaceExportCts()
    {
        CancelActiveExport();
        var cts = new CancellationTokenSource();
        _exportCts = cts;
        return cts;
    }

    private void FinishExportCts(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_exportCts, cts))
        {
            _exportCts = null;
        }

        cts.Dispose();
    }

    private void CancelActiveExport()
    {
        CancellationTokenSource? cts = _exportCts;
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private string BuildSuggestedExportFileName(string suffix)
    {
        if (Engine.SourcePath is { Length: > 0 } path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(stem))
            {
                return $"{stem}_{suffix}";
            }
        }

        return suffix;
    }

    private void UpdateBaselineSummaryText()
    {
        BaselineSummaryText = ActiveBaselineMode switch
        {
            BaselineMode.ForImage => Engine.HasImage
                ? $"For Image ({Engine.NativeSize.Width}×{Engine.NativeSize.Height})"
                : "For Image (Auto-analyzed)",
            BaselineMode.ForSize => Engine.HasImage
                ? $"For Size ({Engine.NativeSize.Width}×{Engine.NativeSize.Height})"
                : "For Size (Dimension-scaled)",
            BaselineMode.Default => "Default (Static)",
            _ => ActiveBaselineMode.ToString(),
        };
    }

    private void NotifyActionCanExecuteChanged()
    {
        RunNowCommand.NotifyCanExecuteChanged();
        CancelRenderCommand.NotifyCanExecuteChanged();
        RenderFullResolutionCommand.NotifyCanExecuteChanged();
        ExportFinalFullResCommand.NotifyCanExecuteChanged();
        ExportFinalPreviewResCommand.NotifyCanExecuteChanged();
        ExportAllStagesCommand.NotifyCanExecuteChanged();
    }

    private static string FormatStepTimingText(TimeSpan elapsed, bool fromCache, bool bypassed)
    {
        if (bypassed)
        {
            return "Bypassed";
        }

        if (fromCache)
        {
            return "Cached";
        }

        return elapsed.TotalMilliseconds < 1.0 ? "<1 ms" : $"{elapsed.TotalMilliseconds:0} ms";
    }

    private static string EnumDisplayNameConverterLabel(PreviewResolutionCap cap) => cap switch
    {
        PreviewResolutionCap.Native => "Native",
        PreviewResolutionCap.Px512 => "512 px",
        PreviewResolutionCap.Px768 => "768 px",
        PreviewResolutionCap.Px1024 => "1024 px",
        PreviewResolutionCap.Px1536 => "1536 px",
        _ => cap.ToString(),
    };
}
