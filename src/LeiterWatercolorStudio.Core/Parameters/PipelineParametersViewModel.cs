using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Parameters.Stages;

namespace LeiterWatercolorStudio.Core.Parameters;

/// <summary>
/// Coordinates all seven editable watercolor pipeline stage view models, baseline tracking,
/// undo/redo history, drag transaction coalescing, and option validation.
/// </summary>
public sealed class PipelineParametersViewModel : ObservableObject
{
    private readonly StageViewModelBase[] _stages;

    private BaselineMode _activeBaselineMode;
    private WatercolorOptions _baselineOptions;
    private PaletteMode? _baselinePaletteModeHint;
    private bool _isLoading;

    /// <summary>
    /// Initializes a new <see cref="PipelineParametersViewModel"/> seeded with <see cref="WatercolorOptions.Default"/>.
    /// </summary>
    public PipelineParametersViewModel()
        : this(WatercolorOptions.Default, BaselineMode.Default)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="PipelineParametersViewModel"/> with an explicit baseline configuration.
    /// </summary>
    /// <param name="initialBaseline">The initial baseline <see cref="WatercolorOptions"/>.</param>
    /// <param name="initialMode">The initial <see cref="BaselineMode"/>.</param>
    /// <param name="undoMaxCapacity">The maximum capacity of the undo history stack.</param>
    public PipelineParametersViewModel(
        WatercolorOptions initialBaseline,
        BaselineMode initialMode = BaselineMode.Default,
        int undoMaxCapacity = UndoRedoManager.DefaultMaxCapacity)
    {
        ArgumentNullException.ThrowIfNull(initialBaseline);
        initialBaseline.Validate();
        if (!Enum.IsDefined(initialMode))
        {
            throw new ArgumentOutOfRangeException(nameof(initialMode), initialMode, "Unsupported BaselineMode value.");
        }

        _activeBaselineMode = initialMode;
        _baselineOptions = initialBaseline;

        ColorTransfer = new ColorTransferStageViewModel();
        Saliency = new SaliencyStageViewModel();
        Segmentation = new SegmentationStageViewModel();
        LodAbstraction = new LodAbstractionStageViewModel();
        WetInWet = new WetInWetStageViewModel();
        HandTremor = new HandTremorStageViewModel();
        Finishing = new FinishingStageViewModel();

        _stages =
        [
            ColorTransfer,
            Saliency,
            Segmentation,
            LodAbstraction,
            WetInWet,
            HandTremor,
            Finishing,
        ];

        foreach (StageViewModelBase stage in _stages)
        {
            stage.ParameterChanged += OnStageParameterChanged;
        }

        UndoRedo = new UndoRedoManager(undoMaxCapacity);
        UndoRedo.PropertyChanged += OnUndoRedoPropertyChanged;

        ApplyAllStages(initialBaseline, paletteModeHint: null);
        SyncPerlinBaselines(initialBaseline);
        UndoRedo.Reset(BuildOptions());
    }

    /// <summary>
    /// Raised whenever pipeline parameters change, providing the validated <see cref="WatercolorOptions"/>
    /// and whether the change should be rendered immediately.
    /// </summary>
    public event EventHandler<OptionsChangedEventArgs>? OptionsChanged;

    /// <summary>
    /// Gets the Stage 1 (Color Transfer) view model.
    /// </summary>
    public ColorTransferStageViewModel ColorTransfer { get; }

    /// <summary>
    /// Gets the Stage 2 (Saliency) view model.
    /// </summary>
    public SaliencyStageViewModel Saliency { get; }

    /// <summary>
    /// Gets the Stage 3a (Segmentation) view model.
    /// </summary>
    public SegmentationStageViewModel Segmentation { get; }

    /// <summary>
    /// Gets the Stage 3b (LOD Abstraction) view model.
    /// </summary>
    public LodAbstractionStageViewModel LodAbstraction { get; }

    /// <summary>
    /// Gets the Stage 4 (Wet-in-Wet) view model.
    /// </summary>
    public WetInWetStageViewModel WetInWet { get; }

    /// <summary>
    /// Gets the Stage 5 (Hand Tremor) view model.
    /// </summary>
    public HandTremorStageViewModel HandTremor { get; }

    /// <summary>
    /// Gets the Stage 6 (Finishing) view model.
    /// </summary>
    public FinishingStageViewModel Finishing { get; }

    /// <summary>
    /// Gets the undo/redo snapshot manager.
    /// </summary>
    public UndoRedoManager UndoRedo { get; }

    /// <summary>
    /// Gets the active baseline selection mode.
    /// </summary>
    public BaselineMode ActiveBaselineMode
    {
        get => _activeBaselineMode;
        private set => SetProperty(ref _activeBaselineMode, value);
    }

    /// <summary>
    /// Gets the active baseline <see cref="WatercolorOptions"/> used by reset operations.
    /// </summary>
    public WatercolorOptions BaselineOptions
    {
        get => _baselineOptions;
        private set => SetProperty(ref _baselineOptions, value);
    }

    /// <summary>
    /// Gets whether an undo operation is currently available.
    /// </summary>
    public bool CanUndo => UndoRedo.CanUndo;

    /// <summary>
    /// Gets whether a redo operation is currently available.
    /// </summary>
    public bool CanRedo => UndoRedo.CanRedo;

    /// <summary>
    /// Loads all stage view models from <paramref name="options"/> and raises <see cref="OptionsChanged"/> immediately.
    /// </summary>
    /// <param name="options">The validated options to load.</param>
    /// <param name="recordUndo">
    /// <see langword="true"/> to push the prior state onto the undo stack;
    /// <see langword="false"/> to reset the undo/redo stack to the loaded options.
    /// </param>
    /// <param name="paletteModeHint">Optional explicit <see cref="PaletteMode"/> override for Stage 1.</param>
    public void LoadFromOptions(
        WatercolorOptions options,
        bool recordUndo = false,
        PaletteMode? paletteModeHint = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        ApplyAllStages(options, paletteModeHint);
        WatercolorOptions loaded = BuildOptions();

        if (recordUndo)
        {
            UndoRedo.Record(loaded);
        }
        else
        {
            UndoRedo.Reset(loaded);
        }

        OptionsChanged?.Invoke(this, new OptionsChangedEventArgs(loaded, isImmediate: true));
    }

    /// <summary>
    /// Builds and validates a complete immutable <see cref="WatercolorOptions"/> snapshot from all seven stage view models.
    /// </summary>
    public WatercolorOptions BuildOptions()
    {
        var options = new WatercolorOptions(
            TargetPalette: ColorTransfer.ToTargetPalette(),
            ColorTransferStrength: ColorTransfer.ColorTransferStrength,
            Egbi: Saliency.Egbi.ToOptions(),
            RegionalContrastSaliency: Saliency.RegionalContrastSaliency.ToOptions(),
            SaliencyCut: Saliency.ToSaliencyCutOptions(),
            SaliencyDistanceField: Saliency.SaliencyDistanceField.ToOptions(),
            MeanShift: Segmentation.ToOptions(),
            LodAbstraction: LodAbstraction.ToOptions(),
            WetInWet: WetInWet.ToOptions(),
            HandTremor: HandTremor.ToOptions(),
            EdgeDarkening: Finishing.ToEdgeDarkeningOptions(),
            TurbulenceFlow: Finishing.ToTurbulenceFlowOptions(),
            ToneCurve: Finishing.ToToneCurveOptions(),
            PaperGranulation: Finishing.ToPaperGranulationOptions(),
            Fxaa: Finishing.ToFxaaOptions(),
            EnableFxaa: Finishing.EnableFxaa,
            EgbiBlurSigma: Saliency.EgbiBlurSigma,
            EnableColorTransfer: ColorTransfer.EnableColorTransfer,
            EnableLodAbstraction: LodAbstraction.EnableLodAbstraction,
            EnableWetInWet: WetInWet.EnableWetInWet,
            EnableHandTremor: HandTremor.EnableHandTremor,
            EnableEdgeDarkening: Finishing.EnableEdgeDarkening,
            EnableTurbulenceFlow: Finishing.EnableTurbulenceFlow,
            EnableToneCurve: Finishing.EnableToneCurve,
            EnablePaperGranulation: Finishing.EnablePaperGranulation);

        options.Validate();
        return options;
    }

    /// <summary>
    /// Resets a single pipeline stage back to <see cref="BaselineOptions"/>, recording an undo step if anything changed.
    /// </summary>
    /// <param name="stageId">The stage to reset.</param>
    public void ResetStage(PrimaryStageId stageId)
    {
        if (stageId == PrimaryStageId.Input)
        {
            return;
        }

        _isLoading = true;
        try
        {
            switch (stageId)
            {
                case PrimaryStageId.ColorTransfer:
                    ColorTransfer.LoadFrom(
                        _baselineOptions.EnableColorTransfer,
                        _baselineOptions.TargetPalette,
                        _baselineOptions.ColorTransferStrength,
                        _baselinePaletteModeHint);
                    break;
                case PrimaryStageId.Saliency:
                    Saliency.LoadFrom(
                        _baselineOptions.EgbiBlurSigma,
                        _baselineOptions.Egbi,
                        _baselineOptions.RegionalContrastSaliency,
                        _baselineOptions.SaliencyCut,
                        _baselineOptions.SaliencyDistanceField);
                    break;
                case PrimaryStageId.Segmentation:
                    Segmentation.LoadFrom(_baselineOptions.MeanShift);
                    break;
                case PrimaryStageId.LodAbstraction:
                    LodAbstraction.LoadFrom(_baselineOptions.EnableLodAbstraction, _baselineOptions.LodAbstraction);
                    break;
                case PrimaryStageId.WetInWet:
                    WetInWet.LoadFrom(_baselineOptions.EnableWetInWet, _baselineOptions.WetInWet);
                    break;
                case PrimaryStageId.HandTremor:
                    HandTremor.LoadFrom(_baselineOptions.EnableHandTremor, _baselineOptions.HandTremor);
                    break;
                case PrimaryStageId.Finishing:
                    LoadFinishingFrom(_baselineOptions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stageId), stageId, "Unsupported PrimaryStageId value.");
            }
        }
        finally
        {
            _isLoading = false;
        }

        WatercolorOptions updated = BuildOptions();
        if (UndoRedo.Current is not null && Equals(updated, UndoRedo.Current))
        {
            return;
        }

        UndoRedo.Record(updated);
        OptionsChanged?.Invoke(this, new OptionsChangedEventArgs(updated, isImmediate: true));
    }

    /// <summary>
    /// Resets all seven editable stages back to <see cref="BaselineOptions"/>, recording an undo step if changed.
    /// </summary>
    public void ResetAllToBaseline()
    {
        WatercolorOptions current = BuildOptions();
        ApplyAllStages(_baselineOptions, _baselinePaletteModeHint);
        WatercolorOptions updated = BuildOptions();

        if (Equals(current, updated))
        {
            return;
        }

        UndoRedo.Record(updated);
        OptionsChanged?.Invoke(this, new OptionsChangedEventArgs(updated, isImmediate: true));
    }

    /// <summary>
    /// Switches <see cref="ActiveBaselineMode"/> and <see cref="BaselineOptions"/>, and loads the new baseline
    /// into all stages (recording undo history).
    /// </summary>
    public void SwitchBaseline(BaselineMode mode, WatercolorOptions baselineOptions) =>
        SwitchBaseline(mode, baselineOptions, paletteModeHint: null);

    /// <summary>
    /// Switches <see cref="ActiveBaselineMode"/> and <see cref="BaselineOptions"/>, and loads the new baseline
    /// into all stages with an optional <paramref name="paletteModeHint"/> (recording undo history).
    /// </summary>
    public void SwitchBaseline(BaselineMode mode, WatercolorOptions baselineOptions, PaletteMode? paletteModeHint)
    {
        SetBaselineOnly(mode, baselineOptions, paletteModeHint);
        LoadFromOptions(baselineOptions, recordUndo: true, paletteModeHint: paletteModeHint);
    }

    /// <summary>
    /// Updates <see cref="ActiveBaselineMode"/>, <see cref="BaselineOptions"/>, and per-noise baselines
    /// without altering current parameter values, undo history, or triggering <see cref="OptionsChanged"/>.
    /// </summary>
    public void SetBaselineOnly(BaselineMode mode, WatercolorOptions baselineOptions) =>
        SetBaselineOnly(mode, baselineOptions, paletteModeHint: null);

    /// <summary>
    /// Updates <see cref="ActiveBaselineMode"/>, <see cref="BaselineOptions"/>, baseline palette mode hint,
    /// and per-noise baselines without altering current parameter values, undo history, or triggering <see cref="OptionsChanged"/>.
    /// </summary>
    public void SetBaselineOnly(BaselineMode mode, WatercolorOptions baselineOptions, PaletteMode? paletteModeHint)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported BaselineMode value.");
        }

        ArgumentNullException.ThrowIfNull(baselineOptions);
        baselineOptions.Validate();

        ActiveBaselineMode = mode;
        BaselineOptions = baselineOptions;
        _baselinePaletteModeHint = paletteModeHint;
        SyncPerlinBaselines(baselineOptions);
    }

    /// <summary>
    /// Begins a coalesced drag transaction on <see cref="UndoRedo"/>.
    /// </summary>
    public void BeginDrag() => UndoRedo.BeginTransaction();

    /// <summary>
    /// Commits the active drag transaction on <see cref="UndoRedo"/> with the current parameter snapshot.
    /// </summary>
    public void EndDrag()
    {
        if (!UndoRedo.IsInTransaction)
        {
            return;
        }

        UndoRedo.CommitTransaction(BuildOptions());
    }

    /// <summary>
    /// Reverts all stage view models to the previous snapshot in <see cref="UndoRedo"/>, if available.
    /// </summary>
    public void Undo()
    {
        if (!UndoRedo.CanUndo)
        {
            return;
        }

        WatercolorOptions previous = UndoRedo.Undo();
        ApplyAllStages(previous, paletteModeHint: null);
        OptionsChanged?.Invoke(this, new OptionsChangedEventArgs(BuildOptions(), isImmediate: true));
    }

    /// <summary>
    /// Re-applies the next snapshot in <see cref="UndoRedo"/>, if available.
    /// </summary>
    public void Redo()
    {
        if (!UndoRedo.CanRedo)
        {
            return;
        }

        WatercolorOptions next = UndoRedo.Redo();
        ApplyAllStages(next, paletteModeHint: null);
        OptionsChanged?.Invoke(this, new OptionsChangedEventArgs(BuildOptions(), isImmediate: true));
    }

    private void ApplyAllStages(WatercolorOptions options, PaletteMode? paletteModeHint)
    {
        _isLoading = true;
        try
        {
            ColorTransfer.LoadFrom(
                options.EnableColorTransfer,
                options.TargetPalette,
                options.ColorTransferStrength,
                paletteModeHint);
            Saliency.LoadFrom(
                options.EgbiBlurSigma,
                options.Egbi,
                options.RegionalContrastSaliency,
                options.SaliencyCut,
                options.SaliencyDistanceField);
            Segmentation.LoadFrom(options.MeanShift);
            LodAbstraction.LoadFrom(options.EnableLodAbstraction, options.LodAbstraction);
            WetInWet.LoadFrom(options.EnableWetInWet, options.WetInWet);
            HandTremor.LoadFrom(options.EnableHandTremor, options.HandTremor);
            LoadFinishingFrom(options);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadFinishingFrom(WatercolorOptions options)
    {
        Finishing.LoadFrom(
            options.EnableEdgeDarkening,
            options.EdgeDarkening,
            options.EnableTurbulenceFlow,
            options.TurbulenceFlow,
            options.EnableToneCurve,
            options.ToneCurve,
            options.EnablePaperGranulation,
            options.PaperGranulation,
            options.EnableFxaa,
            options.Fxaa);
    }

    private void SyncPerlinBaselines(WatercolorOptions baseline)
    {
        HandTremor.PrimaryNoise.SetBaseline(baseline.HandTremor.PrimaryNoise);
        HandTremor.SecondaryNoise.SetBaseline(baseline.HandTremor.SecondaryNoise);
        Finishing.TurbulenceNoise.SetBaseline(baseline.TurbulenceFlow.Noise);
        Finishing.GrainNoise.SetBaseline(baseline.PaperGranulation.GrainNoise);
    }

    private void OnStageParameterChanged(bool isImmediate)
    {
        if (_isLoading)
        {
            return;
        }

        WatercolorOptions current = BuildOptions();
        if (UndoRedo.Current is not null && Equals(current, UndoRedo.Current))
        {
            return;
        }

        UndoRedo.Record(current);
        OptionsChanged?.Invoke(this, new OptionsChangedEventArgs(current, isImmediate));
    }

    private void OnUndoRedoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UndoRedoManager.CanUndo) or nameof(UndoRedoManager.CanRedo))
        {
            OnPropertyChanged(e.PropertyName);
        }
    }
}
